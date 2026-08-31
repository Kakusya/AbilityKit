using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Gateway.Abstractions;
using AbilityKit.Protocol.Room;
using Microsoft.Extensions.Logging;
using Orleans;

namespace AbilityKit.Orleans.Gateway.Handlers;

[Core.GatewayHandler(RoomGatewayOpCodes.GetStateSyncDeliveryMetrics)]
public sealed partial class GetStateSyncDeliveryMetricsHandler : GatewayRequestHandlerBase
{
    private readonly IClusterClient _clusterClient;
    private readonly IGatewaySessionRegistry _sessionRegistry;
    private readonly ILogger<GetStateSyncDeliveryMetricsHandler> _logger;

    public GetStateSyncDeliveryMetricsHandler(
        IClusterClient clusterClient,
        IGatewaySessionRegistry sessionRegistry,
        ILogger<GetStateSyncDeliveryMetricsHandler> logger)
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

        var req = WireRoomGatewayBinary.Deserialize<WireGetStateSyncDeliveryMetricsReq>(request.Payload);
        if (string.IsNullOrWhiteSpace(req.SessionToken)
            || string.IsNullOrWhiteSpace(req.BattleId)
            || string.IsNullOrWhiteSpace(req.RoomId))
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
            var room = _clusterClient.GetGrain<IRoomGrain>(req.RoomId);
            var snapshot = await room.GetSnapshotAsync();
            if (!CanReadMetrics(snapshot, mappedRoomId, req.RoomId, req.BattleId, accountId))
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
            var metrics = await observer.GetDeliveryMetricsAsync();
            var wire = ToWireResponse(metrics);
            return GatewayResponse.Ok(request.Seq, WireRoomGatewayBinary.Serialize(in wire).ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Get state sync delivery metrics failed. BattleId={BattleId}, RoomId={RoomId}, ConnectionId={ConnectionId}",
                req.BattleId,
                req.RoomId,
                context.ConnectionId);
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.InternalError);
        }
    }

    internal static bool CanReadMetrics(
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

    internal static WireGetStateSyncDeliveryMetricsRes ToWireResponse(StateSyncDeliveryMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        return new WireGetStateSyncDeliveryMetricsRes
        {
            Success = true,
            ProducedBytes = metrics.ProducedBytes,
            SentBytes = metrics.SentBytes,
            DroppedBytes = metrics.DroppedBytes,
            MergedBytes = metrics.MergedBytes,
            QueueLength = metrics.QueueLength,
            QueueAgeTicks = metrics.QueueAgeTicks,
            BaselineAgeTicks = metrics.BaselineAgeTicks,
            ResyncCount = metrics.ResyncCount,
            Message = string.Empty
        };
    }
}
