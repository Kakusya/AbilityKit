using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AbilityKit.Orleans.Grains.Battle;

public static class StateSyncObserverServiceCollectionExtensions
{
    public static IServiceCollection AddStateSyncObserverOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<StateSyncObserverOptions>()
            .Bind(configuration.GetSection(StateSyncObserverOptions.ConfigurationSection))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<StateSyncObserverOptions>, StateSyncObserverOptionsValidator>();

        return services;
    }

    private sealed class StateSyncObserverOptionsValidator : IValidateOptions<StateSyncObserverOptions>
    {
        public ValidateOptionsResult Validate(string? name, StateSyncObserverOptions options)
        {
            var failures = StateSyncObserverOptionsMapper.GetValidationFailures(options);
            return failures.Count == 0
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail(failures);
        }
    }
}
