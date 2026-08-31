using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Grains.Battle;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AbilityKit.Orleans.Grains.Tests.Battle;

public sealed class BattleInputSecurityOptionsTests
{
    [Fact]
    public void Defaults_MatchExistingInputSecurityPolicy()
    {
        var options = new BattleInputSecurityOptions();

        Assert.Equal(4096, options.MaxPayloadBytes);
        Assert.Equal(65535, options.MaxOpCode);
        Assert.Equal(128, options.ReplayWindowSize);
        Assert.Equal(60, options.InputsPerSecond);
        Assert.Equal(90, options.BurstInputs);
        Assert.Equal(4096, options.MaxGatewayTrackedKeys);
        Assert.Equal(256, options.MaxBattleTrackedPlayers);
        Assert.Equal(300, options.GatewayIdleStateTtlSeconds);
    }

    [Fact]
    public void Binding_MapsAllConfiguredValues()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            [$"{BattleInputSecurityOptions.ConfigurationSection}:MaxPayloadBytes"] = "2048",
            [$"{BattleInputSecurityOptions.ConfigurationSection}:MaxOpCode"] = "32000",
            [$"{BattleInputSecurityOptions.ConfigurationSection}:ReplayWindowSize"] = "64",
            [$"{BattleInputSecurityOptions.ConfigurationSection}:InputsPerSecond"] = "30",
            [$"{BattleInputSecurityOptions.ConfigurationSection}:BurstInputs"] = "45",
            [$"{BattleInputSecurityOptions.ConfigurationSection}:MaxGatewayTrackedKeys"] = "1024",
            [$"{BattleInputSecurityOptions.ConfigurationSection}:MaxBattleTrackedPlayers"] = "128",
            [$"{BattleInputSecurityOptions.ConfigurationSection}:GatewayIdleStateTtlSeconds"] = "120"
        });

        var options = provider.GetRequiredService<IOptions<BattleInputSecurityOptions>>().Value;

        Assert.Equal(2048, options.MaxPayloadBytes);
        Assert.Equal(32000, options.MaxOpCode);
        Assert.Equal(64, options.ReplayWindowSize);
        Assert.Equal(30, options.InputsPerSecond);
        Assert.Equal(45, options.BurstInputs);
        Assert.Equal(1024, options.MaxGatewayTrackedKeys);
        Assert.Equal(128, options.MaxBattleTrackedPlayers);
        Assert.Equal(120, options.GatewayIdleStateTtlSeconds);
    }

    [Theory]
    [InlineData("MaxPayloadBytes")]
    [InlineData("MaxOpCode")]
    [InlineData("ReplayWindowSize")]
    [InlineData("InputsPerSecond")]
    [InlineData("BurstInputs")]
    [InlineData("MaxGatewayTrackedKeys")]
    [InlineData("MaxBattleTrackedPlayers")]
    [InlineData("GatewayIdleStateTtlSeconds")]
    public void Binding_NonPositiveBoundaryThrowsOptionsValidationException(string key)
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            [$"{BattleInputSecurityOptions.ConfigurationSection}:{key}"] = "0"
        });

        var exception = Assert.Throws<OptionsValidationException>(
            () => _ = provider.GetRequiredService<IOptions<BattleInputSecurityOptions>>().Value);

        Assert.Contains(key, exception.Message);
    }

    [Fact]
    public void Snapshot_IsIndependentFromMutableSource()
    {
        var source = new BattleInputSecurityOptions
        {
            ReplayWindowSize = 16,
            BurstInputs = 20
        };
        var snapshot = source.Snapshot();

        source.ReplayWindowSize = 99;
        source.BurstInputs = 100;

        Assert.Equal(16, snapshot.ReplayWindowSize);
        Assert.Equal(20, snapshot.BurstInputs);
    }

    private static ServiceProvider BuildProvider(IReadOnlyDictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        services.AddBattleInputSecurityOptions(configuration);
        return services.BuildServiceProvider();
    }
}
