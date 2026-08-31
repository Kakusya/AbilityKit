namespace AbilityKit.Orleans.Contracts.Battle;

public sealed class BattleInputSecurityOptions
{
    public const string ConfigurationSection = "AbilityKit:BattleInputSecurity";

    public int MaxPayloadBytes { get; set; } = 4096;
    public int MaxOpCode { get; set; } = 65535;
    public int ReplayWindowSize { get; set; } = 128;
    public int InputsPerSecond { get; set; } = 60;
    public int BurstInputs { get; set; } = 90;
    public int MaxGatewayTrackedKeys { get; set; } = 4096;
    public int MaxBattleTrackedPlayers { get; set; } = 256;
    public int GatewayIdleStateTtlSeconds { get; set; } = 300;

    public BattleInputSecurityOptions Snapshot() => new()
    {
        MaxPayloadBytes = MaxPayloadBytes,
        MaxOpCode = MaxOpCode,
        ReplayWindowSize = ReplayWindowSize,
        InputsPerSecond = InputsPerSecond,
        BurstInputs = BurstInputs,
        MaxGatewayTrackedKeys = MaxGatewayTrackedKeys,
        MaxBattleTrackedPlayers = MaxBattleTrackedPlayers,
        GatewayIdleStateTtlSeconds = GatewayIdleStateTtlSeconds
    };

    public static IReadOnlyList<string> GetValidationFailures(BattleInputSecurityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        AddPositiveFailure(failures, nameof(MaxPayloadBytes), options.MaxPayloadBytes);
        AddPositiveFailure(failures, nameof(MaxOpCode), options.MaxOpCode);
        AddPositiveFailure(failures, nameof(ReplayWindowSize), options.ReplayWindowSize);
        AddPositiveFailure(failures, nameof(InputsPerSecond), options.InputsPerSecond);
        AddPositiveFailure(failures, nameof(BurstInputs), options.BurstInputs);
        AddPositiveFailure(failures, nameof(MaxGatewayTrackedKeys), options.MaxGatewayTrackedKeys);
        AddPositiveFailure(failures, nameof(MaxBattleTrackedPlayers), options.MaxBattleTrackedPlayers);
        AddPositiveFailure(failures, nameof(GatewayIdleStateTtlSeconds), options.GatewayIdleStateTtlSeconds);
        return failures;
    }

    private static void AddPositiveFailure(List<string> failures, string name, int value)
    {
        if (value <= 0)
        {
            failures.Add($"{name} must be greater than zero.");
        }
    }
}
