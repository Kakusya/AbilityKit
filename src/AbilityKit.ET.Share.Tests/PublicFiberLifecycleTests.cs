using System.Diagnostics;
using Xunit;
using global::ET;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace AbilityKit.ET.Share.Tests;

public static class TestSceneTypes
{
    public const int Lifecycle = 9001;
}

public sealed class LifecycleEntity : Entity, IAwake
{
}

[Invoke(TestSceneTypes.Lifecycle)]
public sealed class LifecycleFiberInit : AInvokeHandler<FiberInit, ETTask>
{
    public static Scene? Root { get; set; }

    public override ETTask Handle(FiberInit args)
    {
        Root = args.Fiber.Root;
        return ETTask.CompletedTask;
    }
}

[Invoke]
public sealed class LifecycleLogInvoker : AInvokeHandler<LogInvoker, ILog>
{
    public override ILog Handle(LogInvoker args) => new ConsoleLog();
}

public sealed class PublicFiberLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Public_Create_And_Release_Recursively_Dispose_Tree(bool disposeWorld)
    {
        var previousContext = SynchronizationContext.Current;
        var world = World.Instance;
        var worldDisposed = false;
        try
        {
            var manager = StartWorld(world);

            var creation = AsTask(manager.Create(SchedulerType.Main, 1, 0, TestSceneTypes.Lifecycle, "share-lifecycle"));
            PumpUntilCompleted(manager, creation);
            Assert.Equal(1, await creation);
            Assert.Equal(1, manager.Count());
            var root = Assert.IsType<Scene>(LifecycleFiberInit.Root);
            var parent = root.AddChild<LifecycleEntity>();
            var child = parent.AddChild<LifecycleEntity>();
            var component = child.AddComponent<LifecycleEntity>();
            EntityRef<LifecycleEntity> reference = child;
            Assert.False(root.IsDisposed);
            Assert.False(component.IsDisposed);

            if (disposeWorld)
            {
                world.Dispose();
                worldDisposed = true;
            }
            else
            {
                var removal = AsTask(manager.Remove(1));
                PumpUntilCompleted(manager, removal);
                await removal;
                Assert.Equal(0, manager.Count());
            }

            Assert.True(root.IsDisposed);
            Assert.True(parent.IsDisposed);
            Assert.True(child.IsDisposed);
            Assert.True(component.IsDisposed);
            Assert.Null((LifecycleEntity)reference);
            Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), assembly =>
                assembly.GetName().Name is "AbilityKit.Demo.ET.App" or "AbilityKit.Demo.ET.Logic");
        }
        finally
        {
            if (!worldDisposed)
                world.Dispose();
            LifecycleFiberInit.Root = null;
            Fiber.Instance = null;
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    [Fact]
    public async Task Systems_Run_Once_Per_Explicit_Update_And_Stop_After_Disposal()
    {
        var previousContext = SynchronizationContext.Current;
        var world = World.Instance;
        try
        {
            var manager = StartWorld(world);
            var root = await CreateRoot(manager, 1);
            var entity = root.AddChild<SystemLifecycleEntity>();
            var queue = root.Fiber.EntitySystem.GetQueue(typeof(AClassEventSystem<UpdateEvent>));
            Assert.Single(queue);
            Assert.Equal(1, entity.AwakeCount);
            Assert.Equal(new[] { "awake" }, entity.Events);

            // Main scheduling must not advance from wall-clock passage or LateUpdate alone.
            Thread.Sleep(30);
            manager.LateUpdate();
            Assert.Equal(0, entity.UpdateCount);
            for (var tick = 1; tick <= 3; tick++)
            {
                manager.Update();
                Assert.Equal(tick, entity.UpdateCount);
                manager.LateUpdate();
                Assert.Equal(tick, entity.UpdateCount);
                Assert.Single(queue);
            }

            EntityRef<SystemLifecycleEntity> reference = entity;
            entity.Dispose();
            entity.Dispose();
            Assert.Equal(1, entity.DestroyCount);
            Assert.Null((SystemLifecycleEntity)reference);
            manager.Update();
            manager.LateUpdate();
            Assert.Empty(queue);
            Assert.Equal(3, entity.UpdateCount);
            Assert.Equal(new[] { "awake", "update", "update", "update", "destroy" }, entity.Events);
            Assert.All(entity.UpdateFibers, id => Assert.Equal(1, id));
        }
        finally
        {
            world.Dispose();
            RestoreContext(previousContext);
        }
    }

    [Fact]
    public async Task Fibers_Keep_Separate_Queues_And_Removing_One_Does_Not_Stop_The_Other()
    {
        var previousContext = SynchronizationContext.Current;
        var world = World.Instance;
        try
        {
            var manager = StartWorld(world);
            var first = await CreateRoot(manager, 1);
            var second = await CreateRoot(manager, 2);
            var a = first.AddChild<SystemLifecycleEntity>();
            var b = second.AddChild<SystemLifecycleEntity>();
            var queueA = first.Fiber.EntitySystem.GetQueue(typeof(AClassEventSystem<UpdateEvent>));
            var queueB = second.Fiber.EntitySystem.GetQueue(typeof(AClassEventSystem<UpdateEvent>));
            Assert.NotSame(queueA, queueB);
            Assert.Same(a, (Entity)Assert.Single(queueA));
            Assert.Same(b, (Entity)Assert.Single(queueB));
            manager.Update();
            manager.LateUpdate();
            Assert.Equal(1, a.UpdateCount);
            Assert.Equal(1, b.UpdateCount);

            var removal = AsTask(manager.Remove(1));
            PumpUntilCompleted(manager, removal);
            await removal;
            Assert.Equal(1, manager.Count());
            Assert.Equal(1, a.DestroyCount);
            Assert.Equal(0, b.DestroyCount);
            var aBefore = a.UpdateCount;
            var bBefore = b.UpdateCount;
            for (var tick = 0; tick < 3; tick++)
            {
                manager.Update();
                manager.LateUpdate();
            }
            Assert.Equal(aBefore, a.UpdateCount);
            Assert.Equal(bBefore + 3, b.UpdateCount);
            Assert.All(a.UpdateFibers, id => Assert.Equal(1, id));
            Assert.All(b.UpdateFibers, id => Assert.Equal(2, id));
            Assert.False(second.IsDisposed);
            world.Dispose();
            Assert.Equal(1, a.DestroyCount);
            Assert.Equal(1, b.DestroyCount);
        }
        finally
        {
            world.Dispose();
            RestoreContext(previousContext);
        }
    }

    [Fact]
    public async Task Shutdown_And_Restart_Use_Fresh_Singletons_Queues_And_Entities()
    {
        var previousContext = SynchronizationContext.Current;
        var world = World.Instance;
        try
        {
            var oldManager = StartWorld(world);
            var oldSystems = EntitySystemSingleton.Instance;
            var oldRoot = await CreateRoot(oldManager, 1);
            var oldEntity = oldRoot.AddChild<SystemLifecycleEntity>();
            EntityRef<SystemLifecycleEntity> oldReference = oldEntity;
            oldManager.Update();
            oldManager.LateUpdate();
            Assert.Equal(1, oldEntity.UpdateCount);
            var callbacks = 0;
            oldRoot.Fiber.ThreadSynchronizationContext.Post(() => callbacks++);
            world.Dispose();
            Assert.True(oldManager.IsDisposed());
            Assert.True(oldSystems.IsDisposed());
            Assert.Equal(1, oldEntity.DestroyCount);
            Assert.Null(FiberManager.Instance);
            Assert.Null(EntitySystemSingleton.Instance);
            Assert.Null(CodeTypes.Instance);
            Assert.Null(EventSystem.Instance);
            Assert.Null(ObjectPool.Instance);
            Assert.Null(IdGenerater.Instance);
            Assert.Null(TimeInfo.Instance);
            Assert.Null(Options.Instance);
            Assert.Null(Logger.Instance);
            Assert.Null(SceneTypeSingleton.Instance);
            RestoreContext(previousContext);
            Assert.Null(Fiber.Instance);
            Assert.Same(previousContext, SynchronizationContext.Current);

            var oldWorld = world;
            world = World.Instance;
            Assert.NotSame(oldWorld, world);
            var manager = StartWorld(world);
            Assert.NotSame(oldManager, manager);
            Assert.NotSame(oldSystems, EntitySystemSingleton.Instance);
            var root = await CreateRoot(manager, 1);
            var queue = root.Fiber.EntitySystem.GetQueue(typeof(AClassEventSystem<UpdateEvent>));
            Assert.Empty(queue);
            var entity = root.AddChild<SystemLifecycleEntity>();
            Assert.NotSame(oldEntity, entity);
            Assert.Equal(1, entity.AwakeCount);
            Assert.Equal(0, entity.UpdateCount);
            var newCallbacks = 0;
            root.Fiber.ThreadSynchronizationContext.Post(() => newCallbacks++);
            manager.Update();
            manager.LateUpdate();
            Assert.Equal(1, newCallbacks);
            Assert.Equal(1, entity.UpdateCount);
            Assert.Single(queue);
            Assert.Equal(1, oldEntity.UpdateCount);
            Assert.Null((SystemLifecycleEntity)oldReference);
            Assert.Equal(0, callbacks);
            world.Dispose();
            Assert.Equal(1, entity.DestroyCount);
            Assert.Equal(1, oldEntity.DestroyCount);
        }
        finally
        {
            world.Dispose();
            RestoreContext(previousContext);
        }
    }

    private static FiberManager StartWorld(World world)
    {
        world.AddSingleton(new Options { Process = 1 });
        world.AddSingleton<Logger>();
        world.AddSingleton<TimeInfo>();
        world.AddSingleton<SceneTypeSingleton, Type>(typeof(TestSceneTypes));
        world.AddSingleton<ObjectPool>();
        world.AddSingleton<IdGenerater>();
        // Scan only this host's handlers, not Share's loader/NLog handlers.
        world.AddSingleton<CodeTypes, System.Reflection.Assembly[]>(new[] { typeof(LifecycleFiberInit).Assembly });
        world.AddSingleton<EventSystem>();
        world.AddSingleton<EntitySystemSingleton>();
        return world.AddSingleton<FiberManager>();
    }

    private static async Task<Scene> CreateRoot(FiberManager manager, int id)
    {
        var creation = AsTask(manager.Create(SchedulerType.Main, id, 0, TestSceneTypes.Lifecycle, "share-systems"));
        PumpUntilCompleted(manager, creation);
        Assert.Equal(id, await creation);
        return Assert.IsType<Scene>(LifecycleFiberInit.Root);
    }

    private static void RestoreContext(SynchronizationContext? previousContext)
    {
        LifecycleFiberInit.Root = null;
        Fiber.Instance = null;
        SynchronizationContext.SetSynchronizationContext(previousContext);
    }

    private static async Task<int> AsTask(ETTask<int> task) => await task;
    private static async Task AsTask(ETTask task) => await task;

    private static void PumpUntilCompleted(FiberManager manager, Task task)
    {
        var timeout = Stopwatch.StartNew();
        while (!task.IsCompleted && timeout.Elapsed < TimeSpan.FromSeconds(5))
        {
            manager.Update();
            manager.LateUpdate();
            Thread.Yield();
        }
        Assert.True(task.IsCompleted, "Public Fiber operation did not complete within five seconds.");
    }
}
