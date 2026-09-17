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
            var manager = world.AddSingleton<FiberManager>();

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
