namespace AbilityKit.Demo.Shooter.View
{
    internal static class ShooterSnapshotApplyResults
    {
        public static ShooterSnapshotApplyResult FromPureStateResult(ShooterPureStateSnapshotApplyResult result)
        {
            switch (result)
            {
                case ShooterPureStateSnapshotApplyResult.AppliedFullBaseline:
                case ShooterPureStateSnapshotApplyResult.AppliedDelta:
                    return ShooterSnapshotApplyResult.AppliedActorSnapshot;
                case ShooterPureStateSnapshotApplyResult.IgnoredStaleSnapshot:
                    return ShooterSnapshotApplyResult.IgnoredStaleSnapshot;
                case ShooterPureStateSnapshotApplyResult.NeedsFullBaselineResync:
                    return ShooterSnapshotApplyResult.PureStateBaselineResyncNeeded;
                case ShooterPureStateSnapshotApplyResult.UnsupportedVersion:
                    return ShooterSnapshotApplyResult.UnsupportedVersion;
                case ShooterPureStateSnapshotApplyResult.Ignored:
                default:
                    return ShooterSnapshotApplyResult.Ignored;
            }
        }
    }

    public enum ShooterSnapshotApplyResult
    {
        Ignored = 0,
        AppliedActorSnapshot = 1,
        AppliedPackedSnapshot = 2,
        ImportFailed = 3,
        IgnoredStaleSnapshot = 4,
        PureStateBaselineResyncNeeded = 5,
        UnsupportedVersion = 6
    }
}
