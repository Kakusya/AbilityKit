using AbilityKit.Orleans.Gateway.Abstractions;
using AbilityKit.Protocol.Room;
using Orleans;

namespace AbilityKit.Orleans.Gateway.Handlers;

/// <summary>
/// 房间启动战斗 Handler（阶段 4 起公网入口已废弃）。
/// 客户端应改用 BeginLoading -> ReportAssetsLoaded 流程触发权威启动。
/// 保留 handler 注册，使旧客户端收到明确错误而非 UnhandledOpCode。
/// </summary>
[Core.GatewayHandler(RoomGatewayOpCodes.StartBattle)]
public sealed partial class StartRoomBattleHandler : GatewayRequestHandlerBase
{
    // 保留构造函数签名以维持 DI 注册兼容；公网入口已不再调用 Grain。
    public StartRoomBattleHandler(IClusterClient clusterClient)
    {
        _ = clusterClient;
    }

    public override ValueTask<GatewayResponse> HandleAsync(
        GatewayRequest request,
        GatewaySessionContext context,
        CancellationToken cancellationToken)
    {
        var wire = new WireStartRoomBattleRes
        {
            Success = false,
            Message = "StartBattle is deprecated. Use BeginLoading + ReportAssetsLoaded flow."
        };
        var payload = WireRoomGatewayBinary.Serialize(in wire);
        return new ValueTask<GatewayResponse>(
            GatewayResponse.Error(request.Seq, GatewayStatusCode.Conflict, payload.ToArray()));
    }
}
