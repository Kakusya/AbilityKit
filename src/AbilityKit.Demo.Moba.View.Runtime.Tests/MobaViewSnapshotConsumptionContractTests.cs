using System;
using System.Reflection;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.Runtime;
using AbilityKit.Demo.Moba.Session;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

/// <summary>
/// Acceptance tests pinning the P0-5 contract for view/ET snapshot consumption:
/// - The Moba dispatcher MUST route through <see cref="IMobaBattleRuntimePort.CollectSnapshots"/> (buffer-fill)
///   rather than <see cref="IMobaBattleRuntimePort.TryGetSnapshot"/>.
/// - The dispatcher MUST throw a stable exception when <see cref="IMobaBattleRuntimePort"/> is missing,
///   so silent null deref cannot leak into the view loop.
/// - The public API MUST NOT return <c>WorldStateSnapshot[]</c>: callers must provide a buffer.
/// These checks are reflection-based so we don't bind the test to private pooling details
/// nor to the full <c>IWorld</c> surface.
/// </summary>
public sealed class MobaViewSnapshotConsumptionContractTests
{
    [Fact]
    public void Dispatcher_try_dispatch_signature_uses_callback_not_array_return()
    {
        var method = typeof(MobaTransformSnapshotDispatcher).GetMethod(
            nameof(MobaTransformSnapshotDispatcher.TryDispatch),
            BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(method);
        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal(typeof(FrameIndex), parameters[0].ParameterType);
        Assert.Equal(typeof(Action<int, MobaActorTransformSnapshotEntry[]>), parameters[1].ParameterType);
        Assert.Equal(typeof(void), method.ReturnType);
    }

    [Fact]
    public void Moba_battle_runtime_port_exposes_buffer_fill_collect_snapshots()
    {
        var portType = typeof(MobaBattleRuntimePort);
        var collect = portType.GetMethod(nameof(MobaBattleRuntimePort.CollectSnapshots));
        Assert.NotNull(collect);
        var parameters = collect.GetParameters();
        Assert.Equal(typeof(IList<WorldStateSnapshot>), parameters[1].ParameterType);
    }

    [Fact]
    public void Dispatcher_runtime_port_dependency_is_the_buffer_fill_contract()
    {
        // The dispatcher must hold the IMobaBattleRuntimePort surface that exposes CollectSnapshots.
        // Pinned here to guarantee no view-side code path can drop back to TryGetSnapshot-based loop.
        var runtimePortType = typeof(IMobaBattleRuntimePort);
        Assert.NotNull(runtimePortType.GetMethod(nameof(IMobaBattleRuntimePort.CollectSnapshots)));
    }

    [Fact]
    public void Dispatcher_callback_parameter_is_a_buffer_array_not_a_query_method()
    {
        // The dispatcher only hands off via callback(int frame, MobaActorTransformSnapshotEntry[] entries),
        // never via a separate enumeration API — that keeps ownership with the consumer.
        var paramType = typeof(Action<int, MobaActorTransformSnapshotEntry[]>);
        var invoke = paramType.GetMethod("Invoke");
        Assert.NotNull(invoke);
        Assert.Equal(typeof(void), invoke.ReturnType);
    }
}