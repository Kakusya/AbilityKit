namespace AbilityKit.Protocol.Shooter
{
    public enum ShooterStateSyncPayloadKind
    {
        Packed = 1,
        PureState = 2
    }

    public enum ShooterStateSyncCompatibilityStatus
    {
        Compatible = 0,
        UnsupportedOldVersion = 1,
        UnsupportedFutureVersion = 2
    }

    public readonly struct ShooterStateSyncCompatibilityResult
    {
        public ShooterStateSyncCompatibilityResult(
            ShooterStateSyncPayloadKind payloadKind,
            ShooterStateSyncCompatibilityStatus status,
            int requestedVersion,
            int minimumSupportedVersion,
            int currentVersion)
        {
            PayloadKind = payloadKind;
            Status = status;
            RequestedVersion = requestedVersion;
            MinimumSupportedVersion = minimumSupportedVersion;
            CurrentVersion = currentVersion;
        }

        public ShooterStateSyncPayloadKind PayloadKind { get; }

        public ShooterStateSyncCompatibilityStatus Status { get; }

        public int RequestedVersion { get; }

        public int MinimumSupportedVersion { get; }

        public int CurrentVersion { get; }

        public bool IsCompatible => Status == ShooterStateSyncCompatibilityStatus.Compatible;
    }

    public static class ShooterStateSyncCompatibilityPolicy
    {
        public const int MinimumPackedVersion = 2;
        public const int MinimumPureStateVersion = 1;

        public static ShooterStateSyncCompatibilityResult EvaluatePacked(int version)
        {
            return Evaluate(
                ShooterStateSyncPayloadKind.Packed,
                version,
                MinimumPackedVersion,
                ShooterPackedSnapshotCodec.CurrentVersion);
        }

        public static ShooterStateSyncCompatibilityResult EvaluatePureState(int version)
        {
            return Evaluate(
                ShooterStateSyncPayloadKind.PureState,
                version,
                MinimumPureStateVersion,
                ShooterPureStateSyncCodec.CurrentVersion);
        }

        private static ShooterStateSyncCompatibilityResult Evaluate(
            ShooterStateSyncPayloadKind payloadKind,
            int version,
            int minimumSupportedVersion,
            int currentVersion)
        {
            var status = version < minimumSupportedVersion
                ? ShooterStateSyncCompatibilityStatus.UnsupportedOldVersion
                : version > currentVersion
                    ? ShooterStateSyncCompatibilityStatus.UnsupportedFutureVersion
                    : ShooterStateSyncCompatibilityStatus.Compatible;
            return new ShooterStateSyncCompatibilityResult(
                payloadKind,
                status,
                version,
                minimumSupportedVersion,
                currentVersion);
        }
    }
}
