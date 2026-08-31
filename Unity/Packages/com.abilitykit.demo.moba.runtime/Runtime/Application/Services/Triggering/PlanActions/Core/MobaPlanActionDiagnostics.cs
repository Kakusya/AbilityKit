using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Services;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    /// <summary>
    /// 计划动作模块共享的诊断辅助方法。
    /// </summary>
    public static class MobaPlanActionDiagnostics
    {
        public static bool TryResolveRequired<T>(IWorldResolver resolver, string actionName, out T service)
            where T : class
        {
            service = null;
            if (resolver != null && resolver.TryResolve(out service) && service != null)
            {
                return true;
            }

            Skipped(resolver, actionName, $"required service {typeof(T).Name} not resolved");
            return false;
        }

        public static void Skipped(IWorldResolver resolver, string actionName, string reason)
        {
            Write(resolver, MobaRuntimeLogPurpose.Rejection, "skipped", actionName, reason);
        }

        public static void Rejected(IWorldResolver resolver, string actionName, string reason)
        {
            Write(resolver, MobaRuntimeLogPurpose.Rejection, "rejected", actionName, reason);
        }

        public static void Applied(IWorldResolver resolver, string actionName, string message)
        {
            Write(resolver, MobaRuntimeLogPurpose.Runtime, "applied", actionName, message);
        }

        public static void Investigation(IWorldResolver resolver, string actionName, string message)
        {
            Write(resolver, MobaRuntimeLogPurpose.Investigation, "trace", actionName, message);
        }

        public static void ConfiguredActionDebug(IWorldResolver resolver, string actionName, string message)
        {
            Write(resolver, MobaRuntimeLogPurpose.ConfiguredActionDebug, "debug", actionName, message);
        }

        public static void Rejected(string actionName, string reason)
        {
            Rejected(null, actionName, reason);
        }

        public static void Applied(string actionName, string message)
        {
            Applied(null, actionName, message);
        }

        private static void Write(IWorldResolver resolver, MobaRuntimeLogPurpose purpose, string outcome, string actionName, string message)
        {
            if (string.IsNullOrEmpty(actionName)) actionName = "unknown";
            if (string.IsNullOrEmpty(message)) message = outcome;

            var text = $"Plan action {outcome}. action={actionName} {message}";
            if (resolver != null && resolver.TryResolve<IMobaBattleDiagnosticsService>(out var diagnostics) && diagnostics != null)
            {
                var key = GetMetricName(outcome);
                if (purpose == MobaRuntimeLogPurpose.Rejection)
                {
                    diagnostics.Warning(key, text);
                }

                diagnostics.Counter(key);
            }

            if (purpose == MobaRuntimeLogPurpose.Rejection)
            {
                MobaRuntimeLog.Warning(MobaRuntimeLogModule.Triggering, purpose, "PlanAction", text);
            }
            else if (purpose == MobaRuntimeLogPurpose.Investigation)
            {
                MobaRuntimeLog.Debug(MobaRuntimeLogModule.Triggering, purpose, "PlanAction", text);
            }
            else
            {
                MobaRuntimeLog.Info(MobaRuntimeLogModule.Triggering, purpose, "PlanAction", text);
            }
        }

        private static string GetMetricName(string outcome)
        {
            switch (outcome)
            {
                case "skipped":
                    return MobaBattleDiagnosticMetric.PlanActionSkipped;
                case "rejected":
                    return MobaBattleDiagnosticMetric.PlanActionRejected;
                case "applied":
                    return MobaBattleDiagnosticMetric.PlanActionApplied;
                case "trace":
                case "debug":
                    return MobaBattleDiagnosticMetric.PlanActionTrace;
                default:
                    return MobaBattleDiagnosticMetric.PlanActionTrace;
            }
        }
    }
}
