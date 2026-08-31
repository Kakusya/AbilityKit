using AbilityKit.Orleans.Contracts.Accounts;
using AbilityKit.Orleans.Gateway.Abstractions;
using AbilityKit.Orleans.Gateway.Serialization;
using AbilityKit.Protocol.Room;
using Orleans;

namespace AbilityKit.Orleans.Gateway.Handlers;

/// <summary>
/// Guest 登录 Handler
/// </summary>
[Core.GatewayHandler(RoomGatewayOpCodes.GuestLogin)]
public sealed partial class GuestLoginHandler : GatewayRequestHandlerBase
{
    private readonly IClusterClient _clusterClient;
    private readonly Core.GatewaySessionBinder _sessionBinder;

    public GuestLoginHandler(
        IClusterClient clusterClient,
        Core.GatewaySessionBinder sessionBinder)
    {
        _clusterClient = clusterClient;
        _sessionBinder = sessionBinder;
    }

    public override async ValueTask<GatewayResponse> HandleAsync(
        GatewayRequest request,
        GatewaySessionContext context,
        CancellationToken cancellationToken)
    {
        if (request.Payload == null || request.Payload.Length == 0)
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);

        var req = GatewaySerializer.Deserialize<WireRoomGuestLoginReq>(request.Payload);
        if (string.IsNullOrEmpty(req.GuestId))
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);

        var session = _clusterClient.GetGrain<ISessionGrain>("global");
        var resp = await session.CreateGuestAsync();

        _sessionBinder.Bind(context, resp.AccountId, resp.SessionToken);

        var responsePayload = GatewaySerializer.Serialize(new WireRoomGuestLoginRes
        {
            SessionToken = resp.SessionToken,
            AccountId = resp.AccountId,
            Success = true,
            Message = string.Empty
        });

        return GatewayResponse.Ok(request.Seq, responsePayload.ToArray());
    }
}

