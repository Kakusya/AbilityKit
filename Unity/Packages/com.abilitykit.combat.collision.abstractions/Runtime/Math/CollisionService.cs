using AbilityKit.Ability.World.Services;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Combat.Collision
{
    /// <summary>
    /// 碰撞服务实现
    /// </summary>
    public sealed class CollisionService : ICollisionService, IService
    {
        private readonly ICollisionWorld _world;

        /// <summary>
        /// 使用默认配置创建碰撞服务
        /// </summary>
        public CollisionService()
        {
            _world = CollisionWorldFactory.Create();
        }

        /// <summary>
        /// 使用指定配置创建碰撞服务
        /// </summary>
        public CollisionService(CollisionWorldOptions options)
        {
            _world = CollisionWorldFactory.Create(options);
        }

        public ICollisionWorld World => _world;

        public void Dispose()
        {
        }
    }
}
