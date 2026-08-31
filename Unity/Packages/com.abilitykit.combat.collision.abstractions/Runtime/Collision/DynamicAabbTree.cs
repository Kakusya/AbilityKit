using System;

namespace AbilityKit.Combat.Collision
{
    using Aabb = AbilityKit.Core.Mathematics.Aabb;
    using Vec3 = AbilityKit.Core.Mathematics.Vec3;

    /// <summary>
    /// 动态 AABB 树广相
    ///
    /// 设计说明：
    /// - 基于 Binary AABB Tree，每个节点存储一个 AABB
    /// - 子节点的 AABB 包含在父节点内
    /// - 支持动态对象的更新（通过 fat AABB 机制避免频繁重建）
    /// - 查询效率：O(log n) 平均，最坏 O(n)
    ///
    /// 使用场景：
    /// - 动态对象较多的场景
    /// - 需要高效空间查询的场景
    /// - 与 GridBroadphase 对比：更适合非均匀分布的对象
    /// </summary>
    public sealed class DynamicAabbTree : IBroadphase
    {
        private const float FatCoefficient = 1.1f;

        private TreeNode[] _nodes;
        private int _nodeCount;
        private readonly System.Collections.Generic.Dictionary<int, int> _colliderToNode;
        private int _rootId;

        private struct TreeNode
        {
            public Aabb FatAabb;
            public Aabb OriginalAabb;
            public int ColliderId;
            public int ParentId;
            public int LeftChildId;
            public int RightChildId;
            public int Height;
            public bool IsLeaf;
        }

        public DynamicAabbTree(int initialCapacity = 64)
        {
            _nodes = new TreeNode[initialCapacity];
            _nodeCount = 0;
            _colliderToNode = new System.Collections.Generic.Dictionary<int, int>(initialCapacity);
            _rootId = -1;
        }

        public void Clear()
        {
            _nodes = new TreeNode[64];
            _nodeCount = 0;
            _colliderToNode.Clear();
            _rootId = -1;
        }

        public void Update(int colliderId, in Aabb worldAabb)
        {
            if (_colliderToNode.TryGetValue(colliderId, out var existingNodeId))
            {
                var node = _nodes[existingNodeId];
                var displacement = worldAabb.Center - node.OriginalAabb.Center;
                var margin = node.OriginalAabb.Extents * 0.1f;

                if (System.Math.Abs(displacement.X) < margin.X &&
                    System.Math.Abs(displacement.Y) < margin.Y &&
                    System.Math.Abs(displacement.Z) < margin.Z)
                {
                    return;
                }

                Remove(colliderId);
            }

            var fatAabb = CreateFatAabb(in worldAabb);
            var leafId = AllocateNode();

            _nodes[leafId] = new TreeNode
            {
                FatAabb = fatAabb,
                OriginalAabb = worldAabb,
                ColliderId = colliderId,
                Height = 0,
                IsLeaf = true,
                ParentId = -1,
                LeftChildId = -1,
                RightChildId = -1
            };

            _colliderToNode[colliderId] = leafId;

            if (_rootId == -1)
            {
                _rootId = leafId;
                _nodes[leafId].ParentId = -1;
                return;
            }

            InsertLeaf(leafId);
        }

        public void Remove(int colliderId)
        {
            if (!_colliderToNode.TryGetValue(colliderId, out var nodeId))
                return;

            _colliderToNode.Remove(colliderId);
            RemoveLeaf(nodeId);
        }

        public int Query(in Aabb queryAabb, int[] results, int maxResults)
        {
            if (results == null || maxResults <= 0 || _rootId == -1)
                return 0;

            var stack = new int[64];
            var stackTop = 0;
            var resultCount = 0;

            stack[stackTop++] = _rootId;

            while (stackTop > 0 && resultCount < maxResults)
            {
                stackTop--;
                var nodeId = stack[stackTop];
                var node = _nodes[nodeId];

                if (!AabbIntersect(in node.FatAabb, in queryAabb))
                    continue;

                if (node.IsLeaf)
                {
                    results[resultCount++] = node.ColliderId;
                }
                else
                {
                    if (stackTop < stack.Length - 1) stack[stackTop++] = node.LeftChildId;
                    if (stackTop < stack.Length - 1) stack[stackTop++] = node.RightChildId;
                }
            }

            return resultCount;
        }

        private int AllocateNode()
        {
            if (_nodeCount >= _nodes.Length)
            {
                var newNodes = new TreeNode[_nodes.Length * 2];
                Array.Copy(_nodes, newNodes, _nodes.Length);
                _nodes = newNodes;
            }

            var id = _nodeCount++;
            _nodes[id] = default;
            return id;
        }

        private void InsertLeaf(int leafId)
        {
            var leaf = _nodes[leafId];
            var leafAabb = leaf.FatAabb;

            if (_rootId == -1)
            {
                _rootId = leafId;
                _nodes[leafId] = leaf;
                _nodes[leafId].ParentId = -1;
                _nodes[leafId].Height = 0;
                return;
            }

            var siblingId = _rootId;
            var sibling = _nodes[siblingId];

            while (!sibling.IsLeaf)
            {
                var leftId = sibling.LeftChildId;
                var rightId = sibling.RightChildId;
                var left = _nodes[leftId];
                var right = _nodes[rightId];

                var combinedAabb = CombineAabb(in sibling.FatAabb, in leafAabb);
                var siblingArea = sibling.FatAabb.SurfaceArea();
                var combinedArea = combinedAabb.SurfaceArea();

                var cost = 2.0f * combinedArea;
                var inheritanceCost = 2.0f * (combinedArea - siblingArea);

                var leftCombinedArea = CombineAabb(in left.FatAabb, in leafAabb).SurfaceArea();
                var leftCost = leftCombinedArea + inheritanceCost;

                var rightCombinedArea = CombineAabb(in right.FatAabb, in leafAabb).SurfaceArea();
                var rightCost = rightCombinedArea + inheritanceCost;

                if (cost < leftCost && cost < rightCost)
                    break;

                siblingId = leftCost < rightCost ? leftId : rightId;
                sibling = _nodes[siblingId];
            }

            var oldParentId = sibling.ParentId;
            var newParentId = AllocateNode();

            _nodes[newParentId] = new TreeNode
            {
                ParentId = oldParentId,
                FatAabb = CombineAabb(in sibling.FatAabb, in leafAabb),
                Height = sibling.Height + 1,
                IsLeaf = false,
                ColliderId = -1,
                LeftChildId = siblingId,
                RightChildId = leafId
            };

            if (oldParentId == -1)
            {
                _rootId = newParentId;
            }
            else
            {
                var oldParent = _nodes[oldParentId];
                if (oldParent.LeftChildId == siblingId)
                    _nodes[oldParentId] = UpdateChild(ref oldParent, newParentId, true);
                else
                    _nodes[oldParentId] = UpdateChild(ref oldParent, newParentId, false);
            }

            _nodes[siblingId] = UpdateParent(ref sibling, newParentId);
            _nodes[leafId] = UpdateParent(ref leaf, newParentId);

            RefitAncestors(leafId);
        }

        private static TreeNode UpdateChild(ref TreeNode parent, int childId, bool isLeft)
        {
            if (isLeft) parent.LeftChildId = childId;
            else parent.RightChildId = childId;
            return parent;
        }

        private static TreeNode UpdateParent(ref TreeNode child, int parentId)
        {
            child.ParentId = parentId;
            return child;
        }

        private void RemoveLeaf(int leafId)
        {
            if (leafId == _rootId)
            {
                _rootId = -1;
                return;
            }

            var leaf = _nodes[leafId];
            var parentId = leaf.ParentId;
            var parent = _nodes[parentId];
            var grandParentId = parent.ParentId;

            var siblingId = parent.LeftChildId == leafId ? parent.RightChildId : parent.LeftChildId;
            var sibling = _nodes[siblingId];

            if (grandParentId == -1)
            {
                _rootId = siblingId;
                _nodes[siblingId] = UpdateParent(ref sibling, -1);
            }
            else
            {
                var grandParent = _nodes[grandParentId];
                if (grandParent.LeftChildId == parentId)
                    _nodes[grandParentId] = UpdateChild(ref grandParent, siblingId, true);
                else
                    _nodes[grandParentId] = UpdateChild(ref grandParent, siblingId, false);

                _nodes[siblingId] = UpdateParent(ref sibling, grandParentId);
                _nodes[siblingId] = UpdateFatAabb(ref _nodes[siblingId], parent.FatAabb);
                _nodes[siblingId] = UpdateHeight(ref _nodes[siblingId], (short)(parent.Height - 1));

                var index = grandParentId;
                while (index != -1)
                {
                    var node = _nodes[index];
                    var leftId = node.LeftChildId;
                    var rightId = node.RightChildId;
                    var left = _nodes[leftId];
                    var right = _nodes[rightId];

                    _nodes[index] = UpdateFatAabb(ref node, CombineAabb(in left.FatAabb, in right.FatAabb));
                    _nodes[index] = UpdateHeight(ref _nodes[index], (short)(1 + System.Math.Max(left.Height, right.Height)));

                    index = _nodes[index].ParentId;
                }
            }
        }

        private static TreeNode UpdateFatAabb(ref TreeNode node, Aabb fatAabb)
        {
            node.FatAabb = fatAabb;
            return node;
        }

        private static TreeNode UpdateHeight(ref TreeNode node, short height)
        {
            node.Height = height;
            return node;
        }

        private void RefitAncestors(int leafId)
        {
            var index = _nodes[leafId].ParentId;

            while (index != -1)
            {
                var node = _nodes[index];
                var leftId = node.LeftChildId;
                var rightId = node.RightChildId;
                var left = _nodes[leftId];
                var right = _nodes[rightId];

                _nodes[index] = UpdateFatAabb(ref node, CombineAabb(in left.FatAabb, in right.FatAabb));
                _nodes[index] = UpdateHeight(ref _nodes[index], (short)(1 + System.Math.Max(left.Height, right.Height)));

                index = _nodes[index].ParentId;
            }
        }

        private static Aabb CreateFatAabb(in Aabb aabb)
        {
            var e = aabb.Extents * (FatCoefficient - 1.0f) * 0.5f;
            return new Aabb(aabb.Min - e, aabb.Max + e);
        }

        private static Aabb CombineAabb(in Aabb a, in Aabb b)
        {
            return new Aabb(
                new Vec3(System.Math.Min(a.Min.X, b.Min.X), System.Math.Min(a.Min.Y, b.Min.Y), System.Math.Min(a.Min.Z, b.Min.Z)),
                new Vec3(System.Math.Max(a.Max.X, b.Max.X), System.Math.Max(a.Max.Y, b.Max.Y), System.Math.Max(a.Max.Z, b.Max.Z)));
        }

        private static bool AabbIntersect(in Aabb a, in Aabb b)
        {
            return a.Min.X <= b.Max.X && a.Max.X >= b.Min.X &&
                   a.Min.Y <= b.Max.Y && a.Max.Y >= b.Min.Y &&
                   a.Min.Z <= b.Max.Z && a.Max.Z >= b.Min.Z;
        }

        /// <summary>
        /// 获取根节点 ID（用于调试）
        /// </summary>
        public int RootId => _rootId;

        /// <summary>
        /// 获取节点数量（用于调试）
        /// </summary>
        public int NodeCount => _nodeCount;
    }
}
