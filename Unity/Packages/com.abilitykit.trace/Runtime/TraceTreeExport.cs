using System;
using System.Collections.Generic;
using System.Linq;

namespace AbilityKit.Trace
{
    public enum TraceExportOrder
    {
        ContextId = 0,
        TreePreOrder = 1
    }

    public readonly struct TraceExportOptions
    {
        public TraceExportOptions(int maxNodes, bool activeOnly, bool includeMetadata)
            : this(maxNodes, activeOnly, includeMetadata, 0, TraceExportOrder.ContextId)
        {
        }

        public TraceExportOptions(int maxNodes, bool activeOnly, bool includeMetadata, int maxDepth, TraceExportOrder order)
        {
            MaxNodes = maxNodes;
            ActiveOnly = activeOnly;
            IncludeMetadata = includeMetadata;
            MaxDepth = maxDepth;
            Order = order;
        }

        public int MaxNodes { get; }
        public bool ActiveOnly { get; }
        public bool IncludeMetadata { get; }
        public int MaxDepth { get; }
        public TraceExportOrder Order { get; }

        public static TraceExportOptions Full => new TraceExportOptions(0, false, true);
        public static TraceExportOptions ActiveOnlyDefault => new TraceExportOptions(0, true, true);
    }

    public readonly struct TraceNodeExportDto
    {
        public TraceNodeExportDto(
            long contextId,
            long rootId,
            long parentId,
            int kind,
            string kindName,
            int endedFrame,
            int endReason,
            int childCount,
            object metadata)
            : this(
                contextId,
                rootId,
                parentId,
                kind,
                kindName,
                0,
                endedFrame,
                endReason,
                childCount,
                metadata,
                endedFrame != 0)
        {
        }

        public TraceNodeExportDto(
            long contextId,
            long rootId,
            long parentId,
            int kind,
            string kindName,
            int createdFrame,
            int endedFrame,
            int endReason,
            int childCount,
            object metadata,
            bool isEnded)
        {
            ContextId = contextId;
            RootId = rootId;
            ParentId = parentId;
            Kind = kind;
            KindName = kindName;
            CreatedFrame = createdFrame;
            EndedFrame = endedFrame;
            EndReason = endReason;
            ChildCount = childCount;
            Metadata = metadata;
            IsEnded = isEnded;
        }

        public long ContextId { get; }
        public long RootId { get; }
        public long ParentId { get; }
        public int Kind { get; }
        public string KindName { get; }
        public int CreatedFrame { get; }
        public int EndedFrame { get; }
        public int EndReason { get; }
        public int ChildCount { get; }
        public object Metadata { get; }
        public bool IsEnded { get; }
        public bool IsRoot => ContextId == RootId;
    }

    public sealed class TraceTreeExportDto
    {
        public TraceTreeExportDto(long rootId, IReadOnlyList<TraceNodeExportDto> nodes, bool truncated)
        {
            RootId = rootId;
            Nodes = nodes ?? Array.Empty<TraceNodeExportDto>();
            Truncated = truncated;
        }

        public long RootId { get; }
        public IReadOnlyList<TraceNodeExportDto> Nodes { get; }
        public bool Truncated { get; }
    }

    public static class TraceTreeExportExtensions
    {
        public static TraceTreeExportDto ExportRoot(this TraceTreeRegistryBase registry, long rootId)
        {
            return ExportRoot(registry, rootId, TraceExportOptions.Full);
        }

        public static TraceTreeExportDto ExportRoot(this TraceTreeRegistryBase registry, long rootId, TraceExportOptions options)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (rootId == 0) return new TraceTreeExportDto(rootId, Array.Empty<TraceNodeExportDto>(), false);

            var snapshots = registry.GetNodeSnapshotsByRoot(rootId);
            return BuildExport(registry, rootId, snapshots, options);
        }

        public static IReadOnlyList<TraceTreeExportDto> ExportRoots(this TraceTreeRegistryBase registry, TraceExportOptions options)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            var roots = options.ActiveOnly ? registry.GetActiveRoots() : registry.GetRootStates();
            var result = new List<TraceTreeExportDto>();
            foreach (var root in roots)
            {
                result.Add(registry.ExportRoot(root.RootId, options));
            }

            return result;
        }

        private static TraceTreeExportDto BuildExport(TraceTreeRegistryBase registry, long rootId, IEnumerable<TraceNodeSnapshot> snapshots, TraceExportOptions options)
        {
            var maxNodes = options.MaxNodes;
            var truncated = false;
            var nodes = new List<TraceNodeExportDto>();
            var snapshotList = snapshots.ToList();
            var depthByContextId = options.MaxDepth > 0
                ? BuildDepthMap(rootId, snapshotList)
                : null;

            foreach (var snapshot in OrderSnapshots(rootId, snapshotList, options.Order))
            {
                if (depthByContextId != null && depthByContextId.TryGetValue(snapshot.ContextId, out var depth) && depth > options.MaxDepth)
                {
                    truncated = true;
                    continue;
                }

                if (maxNodes > 0 && nodes.Count >= maxNodes)
                {
                    truncated = true;
                    break;
                }

                nodes.Add(ToDto(registry, in snapshot, options.IncludeMetadata));
            }

            return new TraceTreeExportDto(rootId, nodes, truncated);
        }

        private static IEnumerable<TraceNodeSnapshot> OrderSnapshots(long rootId, IReadOnlyList<TraceNodeSnapshot> snapshots, TraceExportOrder order)
        {
            if (order == TraceExportOrder.TreePreOrder)
            {
                var childrenByParent = snapshots
                    .Where(item => item.ParentId != 0)
                    .GroupBy(item => item.ParentId)
                    .ToDictionary(item => item.Key, item => item.OrderBy(child => child.ContextId).ToList());
                var snapshotByContext = snapshots.ToDictionary(item => item.ContextId);
                var visited = new HashSet<long>();

                if (snapshotByContext.TryGetValue(rootId, out var root))
                {
                    foreach (var item in TraverseTree(root, childrenByParent, visited))
                    {
                        yield return item;
                    }
                }

                foreach (var item in snapshots.OrderBy(item => item.ContextId))
                {
                    if (visited.Add(item.ContextId))
                        yield return item;
                }

                yield break;
            }

            foreach (var item in snapshots.OrderBy(item => item.ContextId))
            {
                yield return item;
            }
        }

        private static IEnumerable<TraceNodeSnapshot> TraverseTree(TraceNodeSnapshot snapshot, IReadOnlyDictionary<long, List<TraceNodeSnapshot>> childrenByParent, ISet<long> visited)
        {
            if (!visited.Add(snapshot.ContextId))
                yield break;

            yield return snapshot;

            if (!childrenByParent.TryGetValue(snapshot.ContextId, out var children))
                yield break;

            foreach (var child in children)
            {
                foreach (var item in TraverseTree(child, childrenByParent, visited))
                {
                    yield return item;
                }
            }
        }

        private static Dictionary<long, int> BuildDepthMap(long rootId, IReadOnlyList<TraceNodeSnapshot> snapshots)
        {
            var result = new Dictionary<long, int>();
            var childrenByParent = snapshots
                .Where(item => item.ParentId != 0)
                .GroupBy(item => item.ParentId)
                .ToDictionary(item => item.Key, item => item.ToList());

            AssignDepth(rootId, 0, childrenByParent, result);
            return result;
        }

        private static void AssignDepth(long contextId, int depth, IReadOnlyDictionary<long, List<TraceNodeSnapshot>> childrenByParent, IDictionary<long, int> result)
        {
            if (result.ContainsKey(contextId))
                return;

            result[contextId] = depth;
            if (!childrenByParent.TryGetValue(contextId, out var children))
                return;

            foreach (var child in children)
            {
                AssignDepth(child.ContextId, depth + 1, childrenByParent, result);
            }
        }

        private static TraceNodeExportDto ToDto(TraceTreeRegistryBase registry, in TraceNodeSnapshot snapshot, bool includeMetadata)
        {
            return new TraceNodeExportDto(
                snapshot.ContextId,
                snapshot.RootId,
                snapshot.ParentId,
                snapshot.Kind,
                registry.GetKindName(snapshot.Kind),
                snapshot.CreatedFrame,
                snapshot.EndedFrame,
                snapshot.EndReason,
                snapshot.ChildCount,
                includeMetadata ? snapshot.Metadata : null,
                snapshot.IsEnded);
        }
    }
}
