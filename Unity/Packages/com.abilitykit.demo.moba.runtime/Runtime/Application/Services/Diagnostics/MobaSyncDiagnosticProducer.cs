using AbilityKit.Demo.Moba.Diagnostics;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 同步健康诊断事件草稿生成器。
    /// 仅接收稳定原语，避免 MOBA Runtime 依赖网络或表现层契约。
    /// </summary>
    public static class MobaSyncDiagnosticProducer
    {
        public static MobaBattleDiagnosticEventDraft CreateSnapshotReceivedDraft(
            int authoritativeFrame,
            uint stateHash)
        {
            var syncPayload = new BattleDiagnosticSyncSnapshotReceivedPayload(
                authoritativeFrame,
                stateHash);
            var payload = BattleDiagnosticEventPayload.FromSyncSnapshotReceived(
                in syncPayload);

            return new MobaBattleDiagnosticEventDraft(
                BattleDiagnosticEventKind.Sync,
                BattleDiagnosticEventChannel.Sync,
                BattleDiagnosticEventOutcome.Succeeded,
                summary: "kind=SnapshotReceived, authoritativeFrame=" +
                         authoritativeFrame +
                         ", stateHash=" +
                         stateHash,
                payload: payload);
        }
    }
}
