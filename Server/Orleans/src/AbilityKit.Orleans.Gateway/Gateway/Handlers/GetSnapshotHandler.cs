using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Gateway.Abstractions;
using AbilityKit.Protocol.Room;
using Orleans;

namespace AbilityKit.Orleans.Gateway.Handlers;

/// <summary>
/// 查询 Room 当前快照（复用 WireRoomSnapshotRes）。
/// 仅做 token->account 映射并调用 Grain。
/// </summary>
[Core.GatewayHandler(RoomGatewayOpCodes.GetSnapshot)]
public sealed partial class GetSnapshotHandler : GatewayRequestHandlerBase
{
    private readonly IClusterClient _clusterClient;

    public GetSnapshotHandler(IClusterClient clusterClient)
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

        var req = WireRoomGatewayBinary.Deserialize<WireGetSnapshotReq>(request.Payload);
        var roomId = string.IsNullOrWhiteSpace(req.RoomId) ? context.RoomId : req.RoomId;
        if (string.IsNullOrWhiteSpace(req.SessionToken) || string.IsNullOrWhiteSpace(roomId))
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);

        try
        {
            var accountId = await RoomGatewayWireMapper.ValidateAccountAsync(_clusterClient, req.SessionToken);
            if (string.IsNullOrWhiteSpace(accountId))
                return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);

            var room = _clusterClient.GetGrain<IRoomGrain>(roomId);
            var snapshot = await room.GetSnapshotAsync();
            var wire = RoomGatewayWireMapper.ToSnapshotRes(snapshot);
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
