using AbilityKit.Ability.World.Services;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Combat.Collision
{
    public interface ICollisionService : IService
    {
        ICollisionWorld World { get; }
    }
}
