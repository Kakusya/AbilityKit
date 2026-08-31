using System;

namespace AbilityKit.Combat.Collision
{
    /// <summary>
    /// 碰撞世界调试形状数据
    /// 用于编辑器可视化调试
    /// </summary>
    public readonly struct CollisionWorldDebugShape
    {
        public Core.Mathematics.ColliderId Id { get; }
        public Core.Mathematics.ColliderShape WorldShape { get; }
        public int LayerId { get; }

        public CollisionWorldDebugShape(Core.Mathematics.ColliderId id, in Core.Mathematics.ColliderShape worldShape, int layerId)
        {
            Id = id;
            WorldShape = worldShape;
            LayerId = layerId;
        }
    }

    /// <summary>
    /// 碰撞世界调试视图接口
    /// </summary>
    public interface ICollisionWorldDebugView
    {
        int CopyWorldShapes(System.Collections.Generic.List<CollisionWorldDebugShape> results);
    }

    /// <summary>
    /// 碰撞世界层关系接口
    /// 用于外部配置层关系矩阵
    /// </summary>
    public interface ICollisionLayerRelation
    {
        /// <summary>
        /// 设置两个层之间的碰撞关系
        /// </summary>
        void SetRelation(int layerA, int layerB, Core.Mathematics.CollisionResponse response);

        /// <summary>
        /// 获取两个层之间的碰撞关系
        /// </summary>
        Core.Mathematics.CollisionResponse GetRelation(int layerA, int layerB);

        /// <summary>
        /// 检查两个层之间是否应该检测
        /// </summary>
        bool ShouldDetect(int layerA, int layerB);
    }
}
