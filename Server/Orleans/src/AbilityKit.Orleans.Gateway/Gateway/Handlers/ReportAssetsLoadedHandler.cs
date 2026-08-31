using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Gateway.Abstractions;
using AbilityKit.Protocol.Room;
using Orleans;

namespace AbilityKit.Orleans.Gateway.Handlers;

/// <summary>
/// 成员上报资源加载完成（Loading 屏障）。
/// 仅做 token->account 映射并调用 Grain，玩法判断在 RoomGrain 内完成。
/// </summary>
[Core.GatewayHandler(RoomGatewayOpCodes.ReportAssetsLoaded)]
public sealed partial class ReportAssetsLoadedHandler : GatewayRequestHandlerBase
{
    private readonly IClusterClient _clusterClient;

    public ReportAssetsLoadedHandler(IClusterClient clusterClient)
    {
        _clusterClient = clusterClient;
    }

    public override async ValueTask<GatewayResponse> HandleAsync(
        GatewayRequest request,
        GatewaySessionContext context,
        CancellationToken cancellationToken)
    {
        if (request.Payload == null || request.Payload.Length == 0)
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);

        var req = WireRoomGatewayBinary.Deserialize<WireReportAssetsLoadedReq>(request.Payload);
        var roomId = string.IsNullOrWhiteSpace(req.RoomId) ? context.RoomId : req.RoomId;
        if (string.IsNullOrWhiteSpace(req.SessionToken) || string.IsNullOrWhiteSpace(roomId))
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);

        try
        {
            var accountId = await RoomGatewayWireMapper.ValidateAccountAsync(_clusterClient, req.SessionToken);
            if (string.IsNullOrWhiteSpace(accountId))
                return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);

            var room = _clusterClient.GetGrain<IRoomGrain>(roomId);
            var grainReq = RoomGatewayWireMapper.ToReportAssetsLoadedReq(accountId, req);
            var result = await room.ReportAssetsLoadedWithResultAsync(grainReq);

            var snapshot = await room.GetSnapshotAsync();
            var wire = RoomGatewayWireMapper.ToRoomOperationRes(result, snapshot);
            var responsePayload = WireRoomGatewayBinary.Serialize(in wire);

            context.RoomId = roomId;
            context.AccountId = accountId;
            return GatewayResponse.Ok(request.Seq, responsePayload.ToArray());
        }
        catch (Exception exception)
        {
            return RoomGatewayErrorMapper.ToResponse(request.Seq, exception);
        }
    }
}
