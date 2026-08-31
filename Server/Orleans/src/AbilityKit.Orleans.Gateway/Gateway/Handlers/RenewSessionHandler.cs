using AbilityKit.Orleans.Contracts.Accounts;
using AbilityKit.Orleans.Gateway.Abstractions;
using AbilityKit.Orleans.Gateway.Core;
using AbilityKit.Orleans.Gateway.Serialization;
using AbilityKit.Protocol.Room;
using MemoryPack;
using Orleans;

namespace AbilityKit.Orleans.Gateway.Handlers;

[GatewayHandler(RoomGatewayOpCodes.RenewSession)]
public sealed partial class RenewSessionHandler : GatewayRequestHandlerBase
{
    private readonly IClusterClient _clusterClient;
    private readonly GatewaySessionBinder _sessionBinder;

    public RenewSessionHandler(
        IClusterClient clusterClient,
        GatewaySessionBinder sessionBinder)
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

        WireRenewSessionReq wireRequest;
        try
        {
            wireRequest = GatewaySerializer.Deserialize<WireRenewSessionReq>(request.Payload);
        }
        catch (Exception exception) when (exception is MemoryPackSerializationException or ArgumentException or InvalidOperationException)
        {
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);
        }

        if (string.IsNullOrWhiteSpace(wireRequest.SessionToken))
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.BadRequest);

        var session = _clusterClient.GetGrain<ISessionGrain>(GatewayGrainKeys.Global);
        var validation = await session.ValidateAsync(new ValidateSessionRequest(wireRequest.SessionToken));
        if (!validation.IsValid || string.IsNullOrWhiteSpace(validation.AccountId))
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.Unauthorized);

        var renewed = await session.RenewAsync(new RenewSessionRequest(
            wireRequest.SessionToken,
            wireRequest.ExtendSeconds,
            wireRequest.RotateToken));
        if (!renewed.IsValid || string.IsNullOrWhiteSpace(renewed.SessionToken))
            return GatewayResponse.Error(request.Seq, GatewayStatusCode.Unauthorized);

        _sessionBinder.Bind(context, validation.AccountId, renewed.SessionToken);

        var response = new WireRenewSessionRes
        {
            Success = true,
            SessionToken = renewed.SessionToken,
            AccountId = validation.AccountId,
            ExpireAtUnixMs = renewed.ExpireAtUnixMs ?? 0,
            Message = string.Empty
        };
        return GatewayResponse.Ok(request.Seq, GatewaySerializer.Serialize(in response).ToArray());
    }
}
