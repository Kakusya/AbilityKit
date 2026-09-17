using ET;

namespace AbilityKit.Game.Cooking.EtBridge;

public sealed class CookingItemEntity : Entity, IAwake
{
    public ItemId ItemId { get; set; }
    public DefinitionId Definition { get; set; }
    public int Version { get; set; }
    public ItemLocation Location { get; set; } = null!;
}

public sealed class CookingItemRegistryComponent : Entity, IAwake
{
    private readonly Dictionary<ItemId, CookingItemEntity> _items = new();

    public IReadOnlyDictionary<ItemId, CookingItemEntity> Items => _items;

    public CookingItemEntity? Find(ItemId id) =>
        _items.TryGetValue(id, out var entity) && !entity.IsDisposed ? entity : null;

    public void Track(ItemId id, CookingItemEntity entity) => _items[id] = entity;

    public void Forget(ItemId id) => _items.Remove(id);
}

// Internal-host probe: accepts trusted full snapshots for one scope in application order.
// Network epoch/sequence validation belongs upstream; CookingSimulation remains authoritative.
public static class CookingEtProjection
{
    public static CookingItemRegistryComponent ApplySnapshot(Scene scene, CookingSnapshot snapshot)
    {
        CookingItemRegistryComponent registry;
        var existing = scene.GetComponent<CookingItemRegistryComponent>();
        if (existing is null)
        {
            registry = scene.AddComponent<CookingItemRegistryComponent>();
        }
        else
        {
            registry = existing;
            // 快照中不存在的物品：从树中移除（触发递归销毁）。
            foreach (var itemId in registry.Items.Keys.ToArray())
            {
                if (snapshot.Items.Any(item => item.Id == itemId))
                    continue;
                var stale = registry.Find(itemId);
                if (stale is not null)
                {
                    stale.Dispose();
                }
                registry.Forget(itemId);
            }
        }

        foreach (var item in snapshot.Items)
        {
            var entity = registry.Find(item.Id);
            if (entity is null)
            {
                entity = registry.AddChild<CookingItemEntity>();
                entity.ItemId = item.Id;
                registry.Track(item.Id, entity);
            }
            entity.Definition = item.Definition;
            entity.Version = item.Version;
            entity.Location = item.Location;
        }

        return registry;
    }
}
