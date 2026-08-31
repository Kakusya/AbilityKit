using System;

namespace AbilityKit.Combat.Collision
{
    /// <summary>
    /// 广相接口
    ///
    /// 设计说明：
    /// - 广相（Broadphase）负责快速筛选可能相交的碰撞体对
    /// - 所有广相实现都使用 AABB（轴对齐包围盒）进行空间划分
    /// - 对于 OBB（有向包围盒）等非轴对齐形状，广相会使用其保守 AABB
    /// - 精确碰撞检测由 Narrowphase（窄相）在 Broadphase 之后执行
    ///
    /// 使用示例：
    /// ```csharp
    /// // 1. 创建广相
    /// var broadphase = new GridBroadphase(cellSize: 4f);
    ///
    /// // 2. 注册碰撞体（传入世界空间 AABB）
    /// var worldAabb = new Aabb(center - halfExtents, center + halfExtents);
    /// broadphase.Update(colliderId, in worldAabb);
    ///
    /// // 3. 查询可能相交的碰撞体
    /// var candidates = new int[64];
    /// var count = broadphase.Query(in queryAabb, candidates, candidates.Length);
    /// ```
    /// </summary>
    public interface IBroadphase
    {
        /// <summary>
        /// 清空所有数据
        /// </summary>
        void Clear();

        /// <summary>
        /// 更新单个碰撞体的世界 AABB
        ///
        /// 注意：广相只存储 AABB 信息。对于 OBB 等非轴对齐形状，
        /// 调用方需要在传入前先转换为保守 AABB。
        ///
        /// 广相实现会使用此 AABB 进行空间划分和查询。
        /// </summary>
        /// <param name="colliderId">碰撞体唯一标识</param>
        /// <param name="worldAabb">碰撞体在世界空间中的 AABB（非局部空间）</param>
        void Update(int colliderId, in Core.Mathematics.Aabb worldAabb);

        /// <summary>
        /// 移除碰撞体
        /// </summary>
        /// <param name="colliderId">碰撞体唯一标识</param>
        void Remove(int colliderId);

        /// <summary>
        /// 查询与给定 AABB 重叠的所有碰撞体 ID
        ///
        /// 返回的 ID 列表是广相候选集，需要后续窄相（narrowphase）进行精确测试。
        /// 返回的 ID 不保证一定与查询 AABB 真正相交。
        /// </summary>
        /// <param name="queryAabb">查询用的 AABB</param>
        /// <param name="results">结果数组，用于存储候选碰撞体 ID</param>
        /// <param name="maxResults">结果数组的最大容量</param>
        /// <returns>实际返回的候选碰撞体数量</returns>
        int Query(in Core.Mathematics.Aabb queryAabb, int[] results, int maxResults);
    }

    /// <summary>
    /// 广相类型枚举
    /// 用于配置和调试
    /// </summary>
    public enum BroadphaseType
    {
        /// <summary>
        /// 朴素广相，O(n) 遍历所有碰撞体
        /// </summary>
        Naive = 0,

        /// <summary>
        /// 网格空间划分广相，适合均匀分布的碰撞体
        /// </summary>
        Grid = 1,

        /// <summary>
        /// 动态 AABB 树广相，适合动态场景
        /// </summary>
        DynamicAabbTree = 2
    }
}
