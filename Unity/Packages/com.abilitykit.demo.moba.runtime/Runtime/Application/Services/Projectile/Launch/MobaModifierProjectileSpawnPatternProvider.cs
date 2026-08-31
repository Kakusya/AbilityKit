using AbilityKit.Ability.World.Services;
using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;

namespace AbilityKit.Demo.Moba.Services.Projectile.Launch
{
    public sealed class MobaModifierProjectileSpawnPatternProvider : IProjectileSpawnPatternProvider
    {
        private readonly MobaSkillParamModifierService _paramModifiers;
        private readonly ProjectileMO _projectile;
        private readonly IWorldRandom _random;
        private readonly int _baseCountPerShot;
        private readonly float _baseFanAngleDeg;
        private readonly IProjectileSpawnPattern _basePattern;

        public MobaModifierProjectileSpawnPatternProvider(
            MobaSkillParamModifierService paramModifiers,
            ProjectileMO projectile,
            IWorldRandom random,
            int baseCountPerShot,
            float baseFanAngleDeg,
            int requestCountPerShot,
            float requestFanAngleDeg)
        {
            if (baseCountPerShot <= 0) throw new System.ArgumentOutOfRangeException(nameof(baseCountPerShot), baseCountPerShot, "Base projectile count per shot must be positive.");
            if (baseFanAngleDeg < 0f) throw new System.ArgumentOutOfRangeException(nameof(baseFanAngleDeg), baseFanAngleDeg, "Base projectile fan angle cannot be negative.");

            _paramModifiers = paramModifiers;
            _projectile = projectile;
            _random = random;
            _baseCountPerShot = baseCountPerShot;
            _baseFanAngleDeg = baseFanAngleDeg;
            _basePattern = CreatePattern(requestCountPerShot, requestFanAngleDeg);
        }

        public IProjectileSpawnPattern GetPattern(in ProjectileSpawnParams baseSpawn, int frame)
        {
            if (_paramModifiers == null)
            {
                return _basePattern;
            }

            if (baseSpawn.RootActorId <= 0)
            {
                throw new System.InvalidOperationException($"Projectile modifier resolution requires a valid root actor id. rootActorId={baseSpawn.RootActorId}");
            }

            var resolveContext = new MobaModifierResolveContext(
                actorId: baseSpawn.RootActorId,
                launcherActorId: baseSpawn.LauncherActorId);
            var countPerShot = _paramModifiers.Projectile.ResolveCountPerShotFromLauncher(
                resolveContext,
                _baseCountPerShot);
            var fanAngleDeg = _paramModifiers.Projectile.ResolveFanAngleDegFromLauncher(
                resolveContext,
                _baseFanAngleDeg);

            return CreatePattern(countPerShot, fanAngleDeg);
        }

        private IProjectileSpawnPattern CreatePattern(int countPerShot, float fanAngleDeg)
        {
            if (countPerShot <= 0) throw new System.ArgumentOutOfRangeException(nameof(countPerShot), countPerShot, "Projectile count per shot must be positive.");
            if (fanAngleDeg < 0f) throw new System.ArgumentOutOfRangeException(nameof(fanAngleDeg), fanAngleDeg, "Projectile fan angle cannot be negative.");

            var bulletsPerShot = countPerShot;
            var resolvedFanAngleDeg = fanAngleDeg;
            IProjectileSpawnPattern pattern;

            if (bulletsPerShot <= 1)
            {
                pattern = new SingleShotPattern();
            }
            else
            {
                pattern = resolvedFanAngleDeg > 0f
                    ? (IProjectileSpawnPattern)new FanPattern(bulletsPerShot, resolvedFanAngleDeg)
                    : new BurstPattern(bulletsPerShot);
            }

            return new MobaRandomizedProjectileSpawnPattern(pattern, _projectile, _random);
        }
    }
}
