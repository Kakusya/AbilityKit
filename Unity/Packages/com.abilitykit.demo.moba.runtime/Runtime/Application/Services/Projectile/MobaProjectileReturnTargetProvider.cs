using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Core.Mathematics;
using AbilityKit.Ability.World.Services.Attributes;

namespace AbilityKit.Demo.Moba.Services.Projectile
{
    [WorldService(typeof(IProjectileReturnTargetProvider))]
    [WorldService(typeof(IProjectileTrackingTargetProvider))]
    [WorldService(typeof(MobaProjectileReturnTargetProvider))]
    public sealed class MobaProjectileReturnTargetProvider : IProjectileReturnTargetProvider, IProjectileTrackingTargetProvider
    {
        private readonly MobaActorRegistry _registry;

        public MobaProjectileReturnTargetProvider(MobaActorRegistry registry)
        {
            _registry = registry;
        }

        public bool TryGetReturnTargetPosition(int launcherActorId, out Vec3 position)
        {
            position = Vec3.Zero;
            if (launcherActorId <= 0) return false;
            if (_registry == null) return false;
            if (!_registry.TryGet(launcherActorId, out var launcher) || launcher == null) return false;

            if (launcher.hasProjectileLauncher)
            {
                var rootActorId = launcher.projectileLauncher.RootActorId;
                if (rootActorId > 0 && _registry.TryGet(rootActorId, out var root) && root != null && root.hasTransform)
                {
                    position = root.transform.Value.Position;
                    return true;
                }
            }

            if (!launcher.hasTransform) return false;
            position = launcher.transform.Value.Position;
            return true;
        }

        public bool TryGetTrackingTargetPosition(int targetActorId, out Vec3 position)
        {
            position = Vec3.Zero;
            if (targetActorId <= 0 || _registry == null) return false;
            if (!_registry.TryGet(targetActorId, out var target) || target == null || !target.hasTransform) return false;

            position = target.transform.Value.Position;
            return true;
        }

        public void Dispose()
        {
        }
    }
}
