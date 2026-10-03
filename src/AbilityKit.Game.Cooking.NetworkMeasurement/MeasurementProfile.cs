using System.Diagnostics;
using AbilityKit.Game.Cooking.Session;

namespace AbilityKit.Game.Cooking.NetworkMeasurement;

// External observation only: never calls a simulation, hooks GC, or encodes a baseline.
internal sealed class MeasurementProfile
{
    private readonly long[] _lastSequence = new long[2];
    private readonly int[] _observedChanges = new int[2];
    private readonly List<double> _ownerCallMs = new();
    private long _allocationStart, _allocationEnd, _windowStart, _windowEnd;
    private int[] _collectionsStart = new int[3], _collectionsEnd = new int[3];
    private long _observerTimestamp, _observerAllocated;
    internal bool Started { get; private set; }
    internal bool Ended { get; private set; }

    internal void Begin()
    {
        var bytes = GC.GetAllocatedBytesForCurrentThread(); var timestamp = Stopwatch.GetTimestamp();
        Started = true;
        _collectionsStart = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
        _allocationStart = GC.GetTotalAllocatedBytes(precise: true);
        _windowStart = Stopwatch.GetTimestamp();
        RecordOverhead(bytes, timestamp);
    }

    internal void End()
    {
        if (Ended) return;
        var bytes = GC.GetAllocatedBytesForCurrentThread(); var timestamp = Stopwatch.GetTimestamp();
        _allocationEnd = GC.GetTotalAllocatedBytes(precise: true);
        _collectionsEnd = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
        _windowEnd = Stopwatch.GetTimestamp(); Ended = true;
        RecordOverhead(bytes, timestamp);
    }

    internal void Observe(CookingNetworkSessionClient[] clients, bool measured, double ownerCallMs)
    {
        var bytes = GC.GetAllocatedBytesForCurrentThread(); var timestamp = Stopwatch.GetTimestamp();
        if (measured && Started && !Ended) _ownerCallMs.Add(ownerCallMs);
        for (var i = 0; i < clients.Length; i++) {
            var sequence = clients[i].LatestBaseline?.Identity.SnapshotSequence ?? 0;
            if (sequence != _lastSequence[i] && measured && Started && !Ended) _observedChanges[i]++;
            _lastSequence[i] = sequence;
        }
        RecordOverhead(bytes, timestamp);
    }

    private void RecordOverhead(long allocatedBefore, long timestampBefore)
    {
        _observerAllocated += GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        _observerTimestamp += Stopwatch.GetTimestamp() - timestampBefore;
    }

    // Failure evidence never finalizes an incomplete window or throws Summary's guard.
    internal object FailureEvidence() => new { Started, Ended,
        completedWindow = Started && Ended,
        observedOwnerCalls = _ownerCallMs.Count,
        observedBaselineIdentityChangesPerClientLowerBound = _observedChanges,
        allocationStart = Started ? (long?)_allocationStart : null,
        allocationEnd = Ended ? (long?)_allocationEnd : null,
        collectionCountsStart = Started ? _collectionsStart : null,
        collectionCountsEnd = Ended ? _collectionsEnd : null };

    internal object Summary()
    {
        if (!Started || !Ended) throw new InvalidOperationException("Diagnostic window not completed.");
        return new { allThreadsManagedAllocatedBytes = _allocationEnd - _allocationStart,
            gcCollectionCountDeltas = Enumerable.Range(0, 3).Select(i => _collectionsEnd[i] - _collectionsStart[i]).ToArray(),
            observedWindowMilliseconds = (_windowEnd - _windowStart) * 1000.0 / Stopwatch.Frequency,
            observedBaselineIdentityChangesPerClientLowerBound = _observedChanges,
            ownerCallMs = Distribution(_ownerCallMs),
            observerOverheadMilliseconds = _observerTimestamp * 1000.0 / Stopwatch.Frequency,
            observerOwnerManagedAllocatedBytes = _observerAllocated,
            boundaries = "All-thread allocation and generation collections cover this process including both clients, Host and runtime background threads; not a standalone Host. Generation counters are nested, do not sum them as distinct collections. Snapshot observations poll immutable LatestBaseline after owner calls: distinct observed changes only, not actual publication/delivery count. Observer overhead includes polling and precise GC boundary calls over the entire run; post-sample summary/JSON export and latency-field assignments excluded. No forced GC, static hooks or added capture/encode calls." };
    }

    private static object Distribution(IEnumerable<double> values)
    {
        var sorted = values.Order().ToArray();
        double Q(double q) => sorted.Length == 0 ? 0 : sorted[(int)Math.Ceiling(sorted.Length * q) - 1];
        return new { samples = sorted.Length, p50 = Q(.5), p95 = Q(.95), p99 = Q(.99) };
    }
}
