using global::ET;

namespace AbilityKit.ET.Share.Tests;

public sealed class SystemLifecycleEntity : Entity, IAwake, IUpdate, IDestroy
{
    public int AwakeCount { get; set; }
    public int UpdateCount { get; set; }
    public int DestroyCount { get; set; }
    public List<int> UpdateFibers { get; } = new();
    public List<string> Events { get; } = new();
}

[EntitySystem]
public sealed class LifecycleAwakeSystem : AwakeSystem<SystemLifecycleEntity>
{
    protected override void Awake(SystemLifecycleEntity self)
    {
        self.AwakeCount++;
        self.Events.Add("awake");
    }
}

[EntitySystem]
public sealed class LifecycleUpdateSystem : UpdateSystem<SystemLifecycleEntity>
{
    protected override void Update(SystemLifecycleEntity self)
    {
        self.UpdateCount++;
        self.UpdateFibers.Add(Fiber.Instance.Id);
        self.Events.Add("update");
    }
}

[EntitySystem]
public sealed class LifecycleDestroySystem : DestroySystem<SystemLifecycleEntity>
{
    protected override void Destroy(SystemLifecycleEntity self)
    {
        self.DestroyCount++;
        self.Events.Add("destroy");
    }
}
