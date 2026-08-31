namespace AbilityKit.Demo.Moba.Services.EntityConstruction
{
    public static class MobaActorSpawnPostSetupApplier
    {
        public static void Apply(global::ActorEntity entity, in MobaActorSpawnPostSetup setup)
        {
            if (entity == null) return;

            if (setup.SetOwnerLink)
            {
                if (entity.hasOwnerLink) entity.ReplaceOwnerLink(setup.OwnerActorId, setup.RootOwnerActorId);
                else entity.AddOwnerLink(setup.OwnerActorId, setup.RootOwnerActorId);
            }

            if (setup.SetLifetime)
            {
                if (entity.hasLifetime) entity.ReplaceLifetime(setup.LifetimeEndTimeMs);
                else entity.AddLifetime(setup.LifetimeEndTimeMs);
            }

            if (setup.SetSummonMeta)
            {
                if (entity.hasSummonMeta) entity.ReplaceSummonMeta(setup.SummonId, setup.DespawnOnOwnerDie);
                else entity.AddSummonMeta(setup.SummonId, setup.DespawnOnOwnerDie);
            }

            if (setup.SetModelId)
            {
                if (entity.hasModelId) entity.ReplaceModelId(setup.ModelId);
                else entity.AddModelId(setup.ModelId);
            }

            if (setup.SetBrain)
            {
                if (entity.hasActorBrain) entity.ReplaceActorBrain(setup.BrainId, setup.BrainOwnerActorId, setup.BrainSourceKind, setup.BrainSourceId, 0L);
                else entity.AddActorBrain(setup.BrainId, setup.BrainOwnerActorId, setup.BrainSourceKind, setup.BrainSourceId, 0L);
            }

            if (setup.SetFlyingProjectileTag)
            {
                entity.isFlyingProjectileTag = true;
            }

            if (setup.SetProjectileLauncher)
            {
                if (entity.hasProjectileLauncher)
                {
                    entity.ReplaceProjectileLauncher(
                        setup.LauncherId,
                        setup.ProjectileId,
                        setup.ProjectileRootActorId,
                        setup.ProjectileLauncherEndTimeMs,
                        setup.ProjectileLauncherActiveBullets,
                        setup.ProjectileLauncherScheduleId,
                        setup.ProjectileLauncherIntervalFrames,
                        setup.ProjectileLauncherTotalCount);
                }
                else
                {
                    entity.AddProjectileLauncher(
                        setup.LauncherId,
                        setup.ProjectileId,
                        setup.ProjectileRootActorId,
                        setup.ProjectileLauncherEndTimeMs,
                        setup.ProjectileLauncherActiveBullets,
                        setup.ProjectileLauncherScheduleId,
                        setup.ProjectileLauncherIntervalFrames,
                        setup.ProjectileLauncherTotalCount);
                }
            }
        }
    }
}
