using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using Xunit;

namespace AbilityKit.World.DI.Tests;

public sealed class WorldInjectAttributeTests
{
    [Fact]
    public void Resolve_InjectsFieldsAndProperties()
    {
        var builder = new WorldContainerBuilder();
        builder.RegisterType<FieldDependency, FieldDependency>(WorldLifetime.Scoped);
        builder.RegisterType<PropertyDependency, PropertyDependency>(WorldLifetime.Scoped);
        builder.RegisterType<TargetWithMembers, TargetWithMembers>(WorldLifetime.Scoped);

        using var container = builder.Build();
        using var scope = container.CreateScope();

        var target = scope.Resolve<TargetWithMembers>();

        Assert.NotNull(target.FieldDependency);
        Assert.NotNull(target.PropertyDependency);
    }

    [Fact]
    public void Resolve_IgnoresMissingOptionalDependency()
    {
        var builder = new WorldContainerBuilder();
        builder.RegisterType<TargetWithOptionalMember, TargetWithOptionalMember>(WorldLifetime.Scoped);

        using var container = builder.Build();
        using var scope = container.CreateScope();

        var target = scope.Resolve<TargetWithOptionalMember>();

        Assert.Null(target.OptionalDependency);
    }

    [Fact]
    public void Resolve_ThrowsWhenRequiredDependencyIsMissing()
    {
        var builder = new WorldContainerBuilder();
        builder.RegisterType<TargetWithRequiredMember, TargetWithRequiredMember>(WorldLifetime.Scoped);

        using var container = builder.Build();
        using var scope = container.CreateScope();

        var ex = Assert.Throws<InvalidOperationException>(() => scope.Resolve<TargetWithRequiredMember>());

        Assert.Contains("Required world service injection failed", ex.Message);
    }

    [Fact]
    public void Resolve_UsesExplicitServiceType()
    {
        var builder = new WorldContainerBuilder();
        builder.RegisterType<IDependencyContract, ContractDependency>(WorldLifetime.Scoped);
        builder.RegisterType<ExplicitServiceTypeDependency, ExplicitServiceTypeDependency>(WorldLifetime.Scoped);
        builder.RegisterType<TargetWithExplicitServiceType, TargetWithExplicitServiceType>(WorldLifetime.Scoped);

        using var container = builder.Build();
        using var scope = container.CreateScope();

        var target = scope.Resolve<TargetWithExplicitServiceType>();

        // [WorldInject(typeof(ExplicitServiceTypeDependency))] 应该注入 ExplicitServiceTypeDependency
        Assert.IsType<ExplicitServiceTypeDependency>(target.Dependency);
    }

    private sealed class FieldDependency
    {
    }

    private sealed class PropertyDependency
    {
    }

    private sealed class MissingDependency
    {
    }

    private interface IDependencyContract
    {
    }

    private sealed class ContractDependency : IDependencyContract
    {
    }

    private sealed class TargetWithMembers
    {
        [WorldInject] private FieldDependency _fieldDependency = null!;

        [WorldInject]
        public PropertyDependency PropertyDependency { get; private set; } = null!;

        public FieldDependency FieldDependency => _fieldDependency;
    }

    private sealed class TargetWithOptionalMember
    {
        [WorldInject(required: false)]
        public MissingDependency? OptionalDependency { get; private set; }
    }

    private sealed class TargetWithRequiredMember
    {
        [WorldInject] private MissingDependency _requiredDependency = null!;

        public MissingDependency RequiredDependency => _requiredDependency;
    }

    private sealed class ExplicitServiceTypeDependency
    {
    }

    private sealed class TargetWithExplicitServiceType
    {
        [WorldInject(typeof(ExplicitServiceTypeDependency))]
        public ExplicitServiceTypeDependency Dependency { get; private set; } = null!;
    }

    [Fact]
    public void Resolve_CircularDependencyBetweenScopedServices_Throws()
    {
        var builder = new WorldContainerBuilder();

        // ScopedA 需要 ScopedB，ScopedB 需要 ScopedA，形成循环
        builder.RegisterType<IScopedA, ScopedA>(WorldLifetime.Scoped);
        builder.RegisterType<IScopedB, ScopedB>(WorldLifetime.Scoped);

        using var container = builder.Build();
        using var scope = container.CreateScope();

        // 尝试解析 IScopedA 应该抛出循环依赖异常
        // ScopedA 构造函数需要 IScopedB
        // IScopedB 的实现 ScopedB 构造函数需要 IScopedA
        // IScopedA 的实现 ScopedA 构造函数需要 IScopedB -> 循环！
        var ex = Assert.Throws<InvalidOperationException>(() => scope.Resolve<IScopedA>());
        Assert.True(ex.Message.Contains("Circular dependency"), "Expected circular dependency message, got: " + ex.Message);
    }

    private interface IScopedA { }
    private interface IScopedB { }

    private sealed class ScopedA : IScopedA
    {
        public ScopedA(IScopedB b) { }
    }

    private sealed class ScopedB : IScopedB
    {
        public ScopedB(IScopedA a) { }
    }
}
