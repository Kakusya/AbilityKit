using AbilityKit.Orleans.Contracts.Battle;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AbilityKit.Orleans.Grains.Battle;

public static class BattleInputSecurityServiceCollectionExtensions
{
    public static IServiceCollection AddBattleInputSecurityOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<BattleInputSecurityOptions>()
            .Bind(configuration.GetSection(BattleInputSecurityOptions.ConfigurationSection))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<BattleInputSecurityOptions>, BattleInputSecurityOptionsValidator>();

        return services;
    }

    private sealed class BattleInputSecurityOptionsValidator : IValidateOptions<BattleInputSecurityOptions>
    {
        public ValidateOptionsResult Validate(string? name, BattleInputSecurityOptions options)
        {
            var failures = BattleInputSecurityOptions.GetValidationFailures(options);
            return failures.Count == 0
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail(failures);
        }
    }
}
