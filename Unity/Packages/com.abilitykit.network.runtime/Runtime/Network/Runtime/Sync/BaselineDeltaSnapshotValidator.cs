#nullable enable

namespace AbilityKit.Network.Runtime.Sync
{
    public enum BaselineDeltaSnapshotValidationStatus
    {
        AcceptedFullBaseline = 0,
        AcceptedDelta = 1,
        IgnoredStaleSnapshot = 2,
        MissingBaseline = 3,
        BaselineMismatch = 4
    }

    public enum BaselineDeltaSnapshotResyncReason
    {
        None = 0,
        MissingBaseline = 1,
        BaselineMismatch = 2
    }

    public readonly struct BaselineDeltaSnapshotInfo
    {
        public BaselineDeltaSnapshotInfo(int frame, bool isFullBaseline, int baselineFrame, uint baselineHash, uint stateHash)
        {
            Frame = frame;
            IsFullBaseline = isFullBaseline;
            BaselineFrame = baselineFrame;
            BaselineHash = baselineHash;
            StateHash = stateHash;
        }

        public int Frame { get; }

        public bool IsFullBaseline { get; }

        public int BaselineFrame { get; }

        public uint BaselineHash { get; }

        public uint StateHash { get; }
    }

    public readonly struct BaselineDeltaSnapshotValidationResult
    {
        public BaselineDeltaSnapshotValidationResult(
            BaselineDeltaSnapshotValidationStatus status,
            BaselineDeltaSnapshotResyncReason resyncReason,
            int frame,
            int lastAppliedFrame,
            int lastBaselineFrame,
            uint lastBaselineHash)
        {
            Status = status;
            ResyncReason = resyncReason;
            Frame = frame;
            LastAppliedFrame = lastAppliedFrame;
            LastBaselineFrame = lastBaselineFrame;
            LastBaselineHash = lastBaselineHash;
        }

        public BaselineDeltaSnapshotValidationStatus Status { get; }

        public BaselineDeltaSnapshotResyncReason ResyncReason { get; }

        public int Frame { get; }

        public int LastAppliedFrame { get; }

        public int LastBaselineFrame { get; }

        public uint LastBaselineHash { get; }

        public bool Accepted => Status == BaselineDeltaSnapshotValidationStatus.AcceptedFullBaseline || Status == BaselineDeltaSnapshotValidationStatus.AcceptedDelta;

        public bool NeedsFullBaselineResync => Status == BaselineDeltaSnapshotValidationStatus.MissingBaseline || Status == BaselineDeltaSnapshotValidationStatus.BaselineMismatch;
    }

    public sealed class BaselineDeltaSnapshotValidator
    {
        private bool _hasAppliedSnapshot;
        private bool _hasBaseline;

        public int LastAppliedFrame { get; private set; }

        public uint LastAppliedStateHash { get; private set; }

        public int LastBaselineFrame { get; private set; }

        public uint LastBaselineHash { get; private set; }

        public bool NeedsFullBaselineResync { get; private set; }

        public BaselineDeltaSnapshotResyncReason LastResyncReason { get; private set; } = BaselineDeltaSnapshotResyncReason.None;

        public int LastIgnoredFrame { get; private set; } = -1;

        public int LastResyncFrame { get; private set; }

        public uint LastResyncStateHash { get; private set; }

        public BaselineDeltaSnapshotValidationResult Validate(in BaselineDeltaSnapshotInfo snapshot)
        {
            if (_hasAppliedSnapshot && snapshot.Frame <= LastAppliedFrame)
            {
                LastIgnoredFrame = snapshot.Frame;
                return CreateResult(BaselineDeltaSnapshotValidationStatus.IgnoredStaleSnapshot, BaselineDeltaSnapshotResyncReason.None, in snapshot);
            }

            if (snapshot.IsFullBaseline)
            {
                return CreateResult(BaselineDeltaSnapshotValidationStatus.AcceptedFullBaseline, BaselineDeltaSnapshotResyncReason.None, in snapshot);
            }

            if (!_hasAppliedSnapshot || !_hasBaseline)
            {
                MarkResync(BaselineDeltaSnapshotResyncReason.MissingBaseline, in snapshot);
                return CreateResult(BaselineDeltaSnapshotValidationStatus.MissingBaseline, LastResyncReason, in snapshot);
            }

            if (snapshot.BaselineFrame != LastBaselineFrame || snapshot.BaselineHash != LastBaselineHash)
            {
                MarkResync(BaselineDeltaSnapshotResyncReason.BaselineMismatch, in snapshot);
                return CreateResult(BaselineDeltaSnapshotValidationStatus.BaselineMismatch, LastResyncReason, in snapshot);
            }

            return CreateResult(BaselineDeltaSnapshotValidationStatus.AcceptedDelta, BaselineDeltaSnapshotResyncReason.None, in snapshot);
        }

        public void CommitApplied(in BaselineDeltaSnapshotInfo snapshot)
        {
            LastAppliedFrame = snapshot.Frame;
            LastAppliedStateHash = snapshot.StateHash;
            if (snapshot.IsFullBaseline)
            {
                _hasBaseline = true;
                LastBaselineFrame = snapshot.BaselineFrame;
                LastBaselineHash = snapshot.BaselineHash;
                NeedsFullBaselineResync = false;
                LastResyncReason = BaselineDeltaSnapshotResyncReason.None;
                LastResyncFrame = 0;
                LastResyncStateHash = 0u;
            }

            _hasAppliedSnapshot = true;
        }

        public void Reset()
        {
            _hasAppliedSnapshot = false;
            _hasBaseline = false;
            LastAppliedFrame = 0;
            LastAppliedStateHash = 0u;
            LastBaselineFrame = 0;
            LastBaselineHash = 0u;
            NeedsFullBaselineResync = false;
            LastResyncReason = BaselineDeltaSnapshotResyncReason.None;
            LastIgnoredFrame = -1;
            LastResyncFrame = 0;
            LastResyncStateHash = 0u;
        }

        private void MarkResync(BaselineDeltaSnapshotResyncReason reason, in BaselineDeltaSnapshotInfo snapshot)
        {
            NeedsFullBaselineResync = true;
            LastResyncReason = reason;
            LastResyncFrame = snapshot.Frame;
            LastResyncStateHash = snapshot.StateHash;
        }

        private BaselineDeltaSnapshotValidationResult CreateResult(
            BaselineDeltaSnapshotValidationStatus status,
            BaselineDeltaSnapshotResyncReason reason,
            in BaselineDeltaSnapshotInfo snapshot)
        {
            return new BaselineDeltaSnapshotValidationResult(
                status,
                reason,
                snapshot.Frame,
                LastAppliedFrame,
                LastBaselineFrame,
                LastBaselineHash);
        }
    }
}
