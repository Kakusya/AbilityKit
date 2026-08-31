using AbilityKit.Core.Mathematics;

namespace AbilityKit.Combat.Projectile
{
    public enum ProjectileCollisionResponse
    {
        Ignore = 0,
        Hit = 1,
        Block = 2,
    }

    public interface IProjectileHitFilter
    {
        bool ShouldHit(int ownerId, ColliderId collider, int frame);
    }

    public interface IProjectileCollisionResponseResolver
    {
        ProjectileCollisionResponse ResolveCollision(int ownerId, ColliderId collider, int frame);
    }
}
