using System.Collections.Generic;

namespace AbilityKit.Combat.Projectile
{
    public sealed class SingleShotPattern : IProjectileSpawnPattern
    {
        public void Build(in ProjectileSpawnParams baseSpawn, List<ProjectileSpawnParams> results)
        {
            if (results == null) return;
            results.Add(baseSpawn.WithPatternSlot(0, 1));
        }
    }
}
