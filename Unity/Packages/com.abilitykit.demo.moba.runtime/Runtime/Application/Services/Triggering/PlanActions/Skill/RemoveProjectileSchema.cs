using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public sealed class RemoveProjectileSchema : MobaPlanActionSchemaBase<RemoveProjectileArgs>
    {
        public static readonly RemoveProjectileSchema Instance = new RemoveProjectileSchema();

        protected override string ActionName => TriggeringConstants.Actions.RemoveProjectile;

        public override RemoveProjectileArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx)
        {
            return default;
        }

        public override bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        {
            error = null;
            return true;
        }
    }
}
