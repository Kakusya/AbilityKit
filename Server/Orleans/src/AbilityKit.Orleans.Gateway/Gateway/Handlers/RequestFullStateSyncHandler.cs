using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Gateway.Abstractions;
using AbilityKit.Protocol.Room;
using Microsoft.Extensions.Logging;
using Orleans;

namespace AbilityKit.Orleans.Gateway.Handlers;

/// <summary>
/// 璇锋眰鏈嶅姟绔悜褰撳墠杩炴帴瀵瑰簲璐﹀彿鎺ㄩ€佷竴娆″畬鏁寸姸鎬佸揩鐓с€?
/// </summary>
[Core.GatewayHandler(RoomGatewayOpCodes.RequestFullStateSync)]
public sealed partial class RequestFullStateSyncHandler : GatewayRequestHandlerBase
{
    private readonly IClusterClient _clusterClient;
    private readonly IGatewaySessionRegistry _sessionRegistry;
    private readonly ILogger<RequestFullStateSyncHandler> _logger;

    public RequestFullStateSyncHandler(
        IClusterClient clusterClient,
        IGatewaySessionRegistry sessionRegistry,
        ILogger<RequestFullStateSyncHandler> logger)
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
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);

        var req = WireRoomGatewayBinary.Deserialize<WireRequestFullStateSyncReq>(request.Payload);
        if (string.IsNullOrWhiteSpace(req.SessionToken) || string.IsNullOrWhiteSpace(req.BattleId) || string.IsNullOrWhiteSpace(req.RoomId))
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

            context.AccountId = accountId;
            if (context.ConnectionId > 0)
            {
                _sessionRegistry.BindAccount(accountId, context.ConnectionId);
            }

            var observerKey = $"{accountId}:{req.RoomId}";
            var observerGrain = _clusterClient.GetGrain<IStateSyncObserverGrain>(observerKey);
            var battleGrain = _clusterClient.GetGrain<IBattleLogicHostGrain>(req.BattleId);

            _logger.LogInformation(
                "Request full state sync. BattleId={BattleId}, RoomId={RoomId}, AccountId={AccountId}, WorldId={WorldId}, ClientFrame={ClientFrame}, LastAuthoritativeFrame={LastAuthoritativeFrame}, Reason={Reason}",
                req.BattleId,
                req.RoomId,
                accountId,
                req.WorldId,
                req.ClientFrame,
                req.LastAuthoritativeFrame,
                req.Reason);

            await battleGrain.RequestFullSnapshotAsync(
                observerGrain,
                new StateSyncObserverInfo
                {
                    ObserverKey = observerKey,
                    AccountId = accountId,
                    RoomId = req.RoomId
                });

            var wire = new WireRequestFullStateSyncRes
            {
                Success = true,
                Accepted = true,
                Message = "accepted",
                ServerTicks = DateTime.UtcNow.Ticks
            };
            var responsePayload = WireRoomGatewayBinary.Serialize(in wire);
            return GatewayResponse.Ok(request.Seq, responsePayload.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Request full state sync failed. BattleId={BattleId}, RoomId={RoomId}, ConnectionId={ConnectionId}, Reason={Reason}",
                req.BattleId,
                req.RoomId,
                context.ConnectionId,
                req.Reason);
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.InternalError);
        }
    }
}

