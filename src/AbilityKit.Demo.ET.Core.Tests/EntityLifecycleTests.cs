using System.Reflection;
using Xunit;

namespace AbilityKit.Demo.ET.Core.Tests;

public class TestComponent : global::ET.Entity, global::ET.IAwake
{
}

public class EntityLifecycleTests
{
    private static global::ET.Fiber CreateFiber()
    {
        // Fiber 构造函数为 internal；经由真实构造获得 EntitySystem/同步上下文。
        var ctor = typeof(global::ET.Fiber).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null, types: new[] { typeof(int), typeof(int), typeof(int), typeof(string) }, modifiers: null);
        Assert.NotNull(ctor);
        return (global::ET.Fiber)ctor!.Invoke(new object[] { 1, 0, (int)global::ET.SceneType.Main, "et-core-test" });
    }

    [Fact]
    public void Scene_Component_Add_Get_Dispose()
    {
        var launchOptions = global::ET.Logic.ETDemoProcessLaunchOptions.CreateLocalSmokeDefaults();
        global::ET.AbilityKit.Demo.ET.App.DemoEntry.Init(launchOptions);

        var fiber = CreateFiber();
        var scene = new global::ET.Scene(fiber, 1, 1, (int)global::ET.SceneType.Main, "test");
        Assert.False(scene.IsDisposed);
        Assert.NotEqual(0, scene.InstanceId);

        var comp = scene.AddComponent<TestComponent>();
        Assert.Same(comp, scene.GetComponent<TestComponent>());
        Assert.False(comp.IsDisposed);

        scene.Dispose();
        Assert.True(scene.IsDisposed);
        Assert.True(comp.IsDisposed);
    }
}
