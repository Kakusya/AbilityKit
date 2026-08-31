using AbilityKit.Orleans.Contracts.Accounts;
using AbilityKit.Orleans.Gateway.Abstractions;
using AbilityKit.Orleans.Gateway.Serialization;
using AbilityKit.Protocol.Room;
using Orleans;

namespace AbilityKit.Orleans.Gateway.Handlers;

/// <summary>
/// Fixed account 登录 Handler，用于断线后同账号重新登录并恢复房间。
/// </summary>
[Core.GatewayHandler(RoomGatewayOpCodes.AccountLogin)]
public sealed partial class AccountLoginHandler : GatewayRequestHandlerBase
{
    private readonly IClusterClient _clusterClient;
    private readonly Core.GatewaySessionBinder _sessionBinder;

    public AccountLoginHandler(
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

        try
        {
            var req = GatewaySerializer.Deserialize<WireRoomAccountLoginReq>(request.Payload);
            if (string.IsNullOrWhiteSpace(req.AccountId))
                return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);

            var session = _clusterClient.GetGrain<ISessionGrain>(GatewayGrainKeys.Global);
            var resp = await session.CreateSessionForAccountAsync(new CreateSessionForAccountRequest(
                req.AccountId,
                req.ExpireSeconds,
                req.KickExisting));

            _sessionBinder.Bind(context, req.AccountId, resp.SessionToken);

            var responsePayload = GatewaySerializer.Serialize(new WireRoomAccountLoginRes
            {
                Success = true,
                SessionToken = resp.SessionToken,
                AccountId = req.AccountId,
                ExpireAtUnixMs = resp.ExpireAtUnixMs,
                KickedSessionToken = resp.KickedSessionToken ?? string.Empty,
                Message = string.Empty
            });

            return GatewayResponse.Ok(request.Seq, responsePayload.ToArray());
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return RoomGatewayErrorMapper.ToResponse(request.Seq, exception);
        }
    }
}
