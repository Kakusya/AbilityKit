using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Demo.Moba.Systems;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public sealed class EmitSchema : MobaPlanActionSchemaBase<EmitArgs>
    {
        public static readonly EmitSchema Instance = new EmitSchema();

        protected override string ActionName => TriggeringConstants.Actions.Emit;

        public override EmitArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx)
        {
            return new EmitArgs(ReadInt(namedArgs, ctx, 0, "emitter_id", "emitterid", "emitterId"));
        }

        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        {
            return RequireAny(args, "emitterId", out error, "emitter_id", "emitterid");
        }
    }
}
