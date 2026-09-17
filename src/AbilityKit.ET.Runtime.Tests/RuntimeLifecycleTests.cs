using global::ET;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace AbilityKit.ET.Runtime.Tests;

public sealed class Probe : Entity, IAwake, IUpdate, ILateUpdate, IDestroy
{
    public int AwakeCount;
    public int Updates;
    public int LateUpdates;
    public int Destroys;
    public bool Paused;
    public List<int> Fibers = new();
}

[EntitySystem]
public sealed class ProbeAwake : AwakeSystem<Probe>
{
    protected override void Awake(Probe self) => self.AwakeCount++;
}

[EntitySystem]
public sealed class ProbeUpdate : UpdateSystem<Probe>
{
    protected override void Update(Probe self)
    {
        if (!self.Paused) self.Updates++;
        self.Fibers.Add(Fiber.Instance.Id);
    }
}

[EntitySystem]
public sealed class ProbeLateUpdate : LateUpdateSystem<Probe>
{
    protected override void LateUpdate(Probe self) => self.LateUpdates++;
}

[EntitySystem]
public sealed class ProbeDestroy : DestroySystem<Probe>
{
    protected override void Destroy(Probe self) => self.Destroys++;
}

public sealed class InertEntity : Entity { }

public sealed class RuntimeLifecycleTests
{
    private static EtRuntimeHost Start() => new(typeof(Probe).Assembly);

    [Fact]
    public void Explicit_tick_runs_registered_systems_and_disposal_stops_them()
    {
        using var host = Start();
        var root = host.CreateScene(1, "one");
        var entity = root.AddChild<Probe>();
        Assert.Equal(1, entity.AwakeCount);
        Assert.Equal(0, entity.Updates);
        for (var i = 1; i <= 3; i++)
        {
            host.Tick();
            Assert.Equal(i, entity.Updates);
            Assert.Equal(i, entity.LateUpdates);
        }
        Assert.Single(root.Fiber.EntitySystem.GetQueue(typeof(AClassEventSystem<UpdateEvent>)), (EntityRef<Entity>)entity);
        EntityRef<Probe> reference = entity;
        entity.Dispose();
        entity.Dispose();
        host.Tick();
        Assert.Equal(1, entity.Destroys);
        Assert.Equal(3, entity.Updates);
        Assert.Null((Probe)reference);
        Assert.Empty(root.Fiber.EntitySystem.GetQueue(typeof(AClassEventSystem<LateUpdateEvent>)));
        Assert.All(entity.Fibers, id => Assert.Equal(1, id));
    }

    [Fact]
    public void Roots_have_separate_queues_and_recursive_ownership()
    {
        using var host = Start();
        var a = host.CreateScene(1, "first");
        var b = host.CreateScene(2, "second");
        var parent = a.AddChild<Probe>();
        var child = parent.AddChild<Probe>();
        var component = child.AddComponent<Probe>();
        var survivor = b.AddChild<Probe>();
        Assert.NotSame(a.Fiber.EntitySystem, b.Fiber.EntitySystem);
        host.Tick();
        host.RemoveScene(1);
        host.Tick();
        Assert.True(a.IsDisposed);
        Assert.Equal(1, parent.Destroys);
        Assert.Equal(1, child.Destroys);
        Assert.Equal(1, component.Destroys);
        Assert.Equal(2, survivor.Updates);
        Assert.All(survivor.Fibers, id => Assert.Equal(2, id));
        Assert.False(b.IsDisposed);
    }

    [Fact]
    public void Pool_reuse_invalidates_old_reference_and_queue_generation()
    {
        using var host = Start();
        var root = host.CreateScene(1, "pool");
        var old = root.AddChild<Probe>(isFromPool: true);
        var generation = old.InstanceId;
        EntityRef<Probe> reference = old;
        old.Dispose();
        var reused = root.AddChild<Probe>(isFromPool: true);
        Assert.Same(old, reused);
        Assert.NotEqual(generation, reused.InstanceId);
        Assert.Null((Probe)reference);
        host.Tick();
        Assert.Equal(1, reused.Updates);
        Assert.Single(root.Fiber.EntitySystem.GetQueue(typeof(AClassEventSystem<UpdateEvent>)));
    }

    [Fact]
    public void Stage_replacement_keeps_parent_owned_state_and_pause_is_not_destruction()
    {
        using var host = Start();
        var restaurant = host.CreateScene(1, "lifetime-fixture");
        var process = restaurant.AddChild<Probe>();
        var stage = restaurant.AddChild<Probe>();
        var order = stage.AddChild<Probe>();
        host.Tick();
        process.Paused = true;
        stage.Dispose();
        host.Tick();
        Assert.Equal(1, process.Updates);
        Assert.False(process.IsDisposed);
        Assert.True(order.IsDisposed);
        var next = restaurant.AddChild<Probe>();
        process.Paused = false;
        host.Tick();
        Assert.Equal(2, process.Updates);
        Assert.Equal(1, next.Updates);
    }

    [Fact]
    public void Shutdown_restart_restores_context_and_does_not_inherit_callbacks()
    {
        var context = SynchronizationContext.Current;
        var fiber = Fiber.Instance;
        var host = Start();
        var root = host.CreateScene(1, "old");
        var entity = root.AddChild<Probe>();
        var callbacks = 0;
        root.Fiber.ThreadSynchronizationContext.Post(() => callbacks++);
        host.Run(1, _ =>
        {
            Assert.Same(root.Fiber, Fiber.Instance);
            Assert.Same(root.Fiber.ThreadSynchronizationContext, SynchronizationContext.Current);
        });
        Assert.Same(context, SynchronizationContext.Current);
        Assert.Same(fiber, Fiber.Instance);
        host.Dispose();
        host.Dispose();
        Assert.Equal(1, entity.Destroys);
        Assert.Null(EntitySystemSingleton.Instance);
        Assert.Null(ObjectPool.Instance);
        Assert.Null(CodeTypes.Instance);
        Assert.Throws<ObjectDisposedException>(host.Tick);
        using var next = Start();
        var newRoot = next.CreateScene(1, "new");
        var newCallbacks = 0;
        newRoot.Fiber.ThreadSynchronizationContext.Post(() => newCallbacks++);
        next.Tick();
        Assert.Equal(1, newCallbacks);
        Assert.Equal(0, callbacks);
        Assert.Same(context, SynchronizationContext.Current);
        Assert.Same(fiber, Fiber.Instance);
    }

    [Fact]
    public void Host_rejects_duplicate_host_ids_reentry_and_foreign_thread()
    {
        using var host = Start();
        host.CreateScene(1, "one");
        Assert.Throws<InvalidOperationException>(() => Start());
        Assert.Throws<ArgumentException>(() => host.CreateScene(1, "duplicate"));
        Assert.Throws<ArgumentOutOfRangeException>(() => host.CreateScene(0, "invalid"));
        host.Run(1, _ =>
        {
            Assert.Throws<InvalidOperationException>(host.Tick);
            Assert.Throws<InvalidOperationException>(host.Dispose);
        });
        Exception? failure = null;
        var thread = new Thread(() => failure = Record.Exception(host.Tick));
        thread.Start();
        thread.Join();
        Assert.IsType<InvalidOperationException>(failure);
        host.Tick();
    }

    [Fact]
    public void Original_ETTask_async_builder_resumes_on_explicit_frame_finish()
    {
        using var host = Start();
        host.CreateScene(1, "tasks");
        var completed = false;
        ETTask? pending = null;
        host.Run(1, root => pending = Wait(root.Fiber, () => completed = true));
        Assert.False(completed);
        host.Tick();
        Assert.True(completed);
    }

    private static async ETTask Wait(Fiber fiber, Action done)
    {
        await fiber.WaitFrameFinish();
        done();
    }

    [Fact]
    public void Runtime_does_not_reference_demo_or_serialization_packages()
    {
        var references = typeof(Entity).Assembly.GetReferencedAssemblies().Select(x => x.Name!).ToArray();
        Assert.All(references, name => Assert.True(name.StartsWith("System") || name == "netstandard", name));
        Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), a => a.GetName().Name!.StartsWith("AbilityKit.Demo"));
    }
}
