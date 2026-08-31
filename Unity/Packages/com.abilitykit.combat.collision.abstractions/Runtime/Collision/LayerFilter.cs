using System;

namespace AbilityKit.Combat.Collision
{
    /// <summary>
    /// 碰撞层过滤器
    ///
    /// 支持双向层过滤：
    /// - IncludeMask: 要检测的层（位掩码）
    /// - ExcludeMask: 要排除的层（位掩码）
    ///
    /// 层关系判断逻辑：
    /// 1. 如果目标的层在 ExcludeMask 中，排除
    /// 2. 如果 IncludeMask 非零且目标的层不在 IncludeMask 中，排除
    /// 3. 否则，包含
    /// </summary>
    public readonly struct LayerFilter : IEquatable<LayerFilter>
    {
        /// <summary>
        /// 要检测的层掩码（位掩码）。为 0 时表示不限制。
        /// </summary>
        public readonly int IncludeMask;

        /// <summary>
        /// 要排除的层掩码（位掩码）
        /// </summary>
        public readonly int ExcludeMask;

        /// <summary>
        /// 要排除的碰撞体 ID 列表（用于忽略特定实体）
        /// </summary>
        public readonly int[] IgnoredColliders;

        public LayerFilter(int includeMask)
        {
            IncludeMask = includeMask;
            ExcludeMask = 0;
            IgnoredColliders = null;
        }

        public LayerFilter(int includeMask, int[] ignoredColliders)
        {
            IncludeMask = includeMask;
            ExcludeMask = 0;
            IgnoredColliders = ignoredColliders;
        }

        private LayerFilter(int includeMask, int excludeMask, int[] ignoredColliders)
        {
            IncludeMask = includeMask;
            ExcludeMask = excludeMask;
            IgnoredColliders = ignoredColliders;
        }

        /// <summary>
        /// 检查目标的层是否应该被包含
        /// </summary>
        /// <param name="layer">目标层（0-63）</param>
        /// <returns>是否应该包含</returns>
        public bool IsLayerIncluded(int layer)
        {
            if (layer < 0 || layer >= 64)
                return false;

            var bit = 1 << layer;

            // 1. 检查排除
            if ((ExcludeMask & bit) != 0)
                return false;

            // 2. 检查包含（如果 IncludeMask 非零）
            if (IncludeMask != 0 && (IncludeMask & bit) == 0)
                return false;

            return true;
        }

        /// <summary>
        /// 检查两个层之间是否应该检测碰撞（用于层关系矩阵）
        /// </summary>
        /// <param name="layerA">层 A</param>
        /// <param name="layerB">层 B</param>
        /// <returns>是否应该检测碰撞</returns>
        public bool ShouldDetectWith(int layerA, int layerB)
        {
            // 两层都必须被包含
            return IsLayerIncluded(layerA) && IsLayerIncluded(layerB);
        }

        /// <summary>
        /// 检查是否应该忽略特定的碰撞体
        /// </summary>
        public bool ShouldIgnore(int colliderId)
        {
            if (IgnoredColliders == null)
                return false;
            for (var i = 0; i < IgnoredColliders.Length; i++)
            {
                if (IgnoredColliders[i] == colliderId)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 创建带有排除层的过滤器
        /// </summary>
        public static LayerFilter IncludeExclude(int includeMask, int excludeMask)
        {
            return new LayerFilter(includeMask, excludeMask, null);
        }

        /// <summary>
        /// 创建带有排除层和忽略列表的过滤器
        /// </summary>
        public static LayerFilter IncludeExclude(int includeMask, int excludeMask, int[] ignoredColliders)
        {
            return new LayerFilter(includeMask, excludeMask, ignoredColliders);
        }

        /// <summary>
        /// 默认过滤器：包含所有层
        /// </summary>
        public static LayerFilter Default => new LayerFilter(0, 0, null);

        /// <summary>
        /// 空过滤器：排除所有
        /// </summary>
        public static LayerFilter None => new LayerFilter(0, ~0, null);

        public bool Equals(LayerFilter other)
        {
            return IncludeMask == other.IncludeMask &&
                   ExcludeMask == other.ExcludeMask &&
                   IgnoredColliders == other.IgnoredColliders;
        }

        public override bool Equals(object obj) => obj is LayerFilter other && Equals(other);

        public override int GetHashCode()
        {
            return HashCode.Combine(IncludeMask, ExcludeMask, IgnoredColliders);
        }

        /// <summary>
        /// 调试用：返回过滤器的字符串表示
        /// </summary>
        public override string ToString()
        {
            return $"LayerFilter(Include=0x{IncludeMask:X8}, Exclude=0x{ExcludeMask:X8})";
        }
    }
}
