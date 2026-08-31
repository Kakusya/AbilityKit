using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Events.Unit;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    [PlanActionModule(order: MobaPlanActionModuleOrders.AdvanceGameplayCounter)]
    public sealed class AdvanceGameplayCounterPlanActionModule : MobaPlanActionModuleBase<AdvanceGameplayCounterArgs, AdvanceGameplayCounterPlanActionModule>
    {
        protected override IActionSchema<AdvanceGameplayCounterArgs, IWorldResolver> Schema => AdvanceGameplayCounterSchema.Instance;

        protected override void Execute(object triggerArgs, AdvanceGameplayCounterArgs args, ExecCtx<IWorldResolver> ctx)
        {
            if (ctx.Context == null || triggerArgs == null || args.KeyId == 0 || args.ScopePayloadFieldId == 0 || args.Threshold <= 0d)
            {
                return;
            }

            if (ctx.Payloads == null || !TryReadPayloadDouble(ctx, triggerArgs, args.ScopePayloadFieldId, out var scopeRaw))
            {
                return;
            }

            var scopeValue = (int)Math.Round(scopeRaw);
            if (scopeValue == 0)
            {
                return;
            }

            if (!TryResolveRequired(ctx, out MobaGameplayVariableService variables))
            {
                return;
            }

            var scopedKey = args.ResolveScopedKey(scopeValue);
            var nextValue = variables.Add(scopedKey, args.Delta);
            if (nextValue < args.Threshold)
            {
                return;
            }

            variables.Set(scopedKey, args.ResetValue);
            if (args.TriggerId <= 0)
            {
                return;
            }

            if (!TryResolveRequired(ctx, out MobaEffectExecutionService effects))
            {
                return;
            }

            effects.ExecuteRulePlan(args.TriggerId, triggerArgs);
        }

        private static bool TryReadPayloadDouble(ExecCtx<IWorldResolver> ctx, object triggerArgs, int fieldId, out double value)
        {
            switch (triggerArgs)
            {
                case DamageResult damageResult:
                    return ctx.Payloads.TryGetDouble(in damageResult, fieldId, out value);
                case AttackInfo attackInfo:
                    return ctx.Payloads.TryGetDouble(in attackInfo, fieldId, out value);
                case UnitDieEventPayload unitDie:
                    return ctx.Payloads.TryGetDouble(in unitDie, fieldId, out value);
                case SkillCastContext skillCast:
                    return ctx.Payloads.TryGetDouble(in skillCast, fieldId, out value);
                default:
                    value = default;
                    return false;
            }
        }
    }
}
