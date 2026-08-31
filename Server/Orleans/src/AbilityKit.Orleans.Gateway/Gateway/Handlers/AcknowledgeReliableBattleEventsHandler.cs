using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Gateway.Abstractions;
using AbilityKit.Protocol.Room;
using Microsoft.Extensions.Logging;
using Orleans;

namespace AbilityKit.Orleans.Gateway.Handlers;

[Core.GatewayHandler(RoomGatewayOpCodes.AckReliableBattleEvents)]
public sealed partial class AcknowledgeReliableBattleEventsHandler : GatewayRequestHandlerBase
{
    private readonly IClusterClient _clusterClient;
    private readonly IGatewaySessionRegistry _sessionRegistry;
    private readonly ILogger<AcknowledgeReliableBattleEventsHandler> _logger;

    public AcknowledgeReliableBattleEventsHandler(
        IClusterClient clusterClient,
        IGatewaySessionRegistry sessionRegistry,
        ILogger<AcknowledgeReliableBattleEventsHandler> logger)
    {
        _clusterClient = clusterClient;
        _sessionRegistry = sessionRegistry;
        _logger = logger;
    }

    public override async ValueTask<GatewayResponse> HandleAsync(
        GatewayRequest request,
        GatewaySessionContext context,
        CancellationToken cancellationToken)
    {
        if (request.Payload == null || request.Payload.Length == 0)
        {
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);
        }

        var req = WireRoomGatewayBinary.Deserialize<WireAckReliableBattleEventsReq>(request.Payload);
        if (string.IsNullOrWhiteSpace(req.SessionToken)
            || string.IsNullOrWhiteSpace(req.BattleId)
            || string.IsNullOrWhiteSpace(req.RoomId)
            || string.IsNullOrWhiteSpace(req.Epoch)
            || req.AckSequence < 0)
        {
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);
        }

        try
        {
            var accountId = await RoomGatewayWireMapper.ValidateAccountAsync(_clusterClient, req.SessionToken);
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);
            }

            var mapping = _clusterClient.GetGrain<IRoomIdMappingGrain>("global");
            var mappedRoomId = await mapping.TryGetAccountRoomAsync(accountId);
            if (!string.Equals(mappedRoomId, req.RoomId, StringComparison.Ordinal))
            {
                return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);
            }

            var room = _clusterClient.GetGrain<IRoomGrain>(req.RoomId);
            var snapshot = await room.GetSnapshotAsync();
            if (!CanAcknowledge(snapshot, mappedRoomId, req.RoomId, req.BattleId, accountId))
            {
                return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);
            }

            context.AccountId = accountId;
            if (context.ConnectionId > 0)
            {
                _sessionRegistry.BindAccount(accountId, context.ConnectionId);
            }

            var observerKey = $"{accountId}:{req.RoomId}";
            var observer = _clusterClient.GetGrain<IStateSyncObserverGrain>(observerKey);
            var ack = await observer.AcknowledgeReliableEventsAsync(req.BattleId, req.Epoch, req.AckSequence);
            var wire = ToWireResponse(ack);
            return GatewayResponse.Ok(request.Seq, WireRoomGatewayBinary.Serialize(in wire).ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Acknowledge reliable battle events failed. BattleId={BattleId}, RoomId={RoomId}, ConnectionId={ConnectionId}, Epoch={Epoch}, Sequence={Sequence}",
                req.BattleId,
                req.RoomId,
                context.ConnectionId,
                req.Epoch,
                req.AckSequence);
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.InternalError);
        }
    }

    internal static bool CanAcknowledge(
        RoomSnapshot? snapshot,
        string? mappedRoomId,
        string? requestedRoomId,
        string? requestedBattleId,
        string? accountId)
    {
        return snapshot != null
            && !string.IsNullOrWhiteSpace(mappedRoomId)
            && !string.IsNullOrWhiteSpace(requestedRoomId)
            && !string.IsNullOrWhiteSpace(requestedBattleId)
            && !string.IsNullOrWhiteSpace(accountId)
            && string.Equals(mappedRoomId, requestedRoomId, StringComparison.Ordinal)
            && snapshot.Members.Contains(accountId, StringComparer.Ordinal)
            && string.Equals(snapshot.BattleId, requestedBattleId, StringComparison.Ordinal);
    }

    internal static WireAckReliableBattleEventsRes ToWireResponse(ReliableBattleEventAckResult ack)
    {
        ArgumentNullException.ThrowIfNull(ack);

        return new WireAckReliableBattleEventsRes
        {
            Success = ack.Accepted && !ack.RequiresResync,
            AcceptedAckSequence = ack.AcceptedSequence,
            Message = ack.RequiresResync ? "resync required" : string.Empty
        };
    }
}
