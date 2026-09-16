using System.Collections.Frozen;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AbilityKit.Game.Cooking;

public enum CookingNetworkMeasurementTopology
{
    InProcess,
}

public enum CookingNetworkEndpointRole
{
    Host,
    Client,
}

public enum CookingNetworkThresholdStatus
{
    Unset,
    Approved,
}

public enum CookingNetworkOptimizationStrategy
{
    Baseline,
    Interpolation,
    LocalPrediction,
    Correction,
}

public enum CookingNetworkOptimizationGateStatus
{
    BaselineOnly,
    Blocked,
}

public enum CookingNetworkOptimizationBlockReason
{
    None,
    ThresholdsUnset,
    OwnerApprovalMissing,
    RealLanEvidenceMissing,
    FallbackPolicyMissing,
    OptimizationRuntimeNotImplemented,
}

public sealed record CookingNetworkSamplingWindow(int SampleCount, int InvocationsPerSample)
{
    public void Validate()
    {
        if (SampleCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(SampleCount), "Sample count must be positive.");
        if (InvocationsPerSample <= 0)
            throw new ArgumentOutOfRangeException(nameof(InvocationsPerSample), "Invocations per sample must be positive.");
    }
}

public sealed record CookingNetworkWorkload(
    string Id,
    string Version,
    string ConfigurationIdentity,
    string ProtocolIdentity,
    CookingNetworkSamplingWindow SamplingWindow)
{
    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public string CanonicalText()
    {
        Validate();
        var canonical = new CanonicalWorkload(Id, Version, ConfigurationIdentity, ProtocolIdentity,
            SamplingWindow.SampleCount, SamplingWindow.InvocationsPerSample);
        return JsonSerializer.Serialize(canonical, CanonicalJsonOptions);
    }

    public string Sha256() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalText())));

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
            throw new ArgumentException("Workload ID must not be blank.", nameof(Id));
        if (string.IsNullOrWhiteSpace(Version))
            throw new ArgumentException("Workload version must not be blank.", nameof(Version));
        if (string.IsNullOrWhiteSpace(ConfigurationIdentity))
            throw new ArgumentException("Configuration identity must not be blank.", nameof(ConfigurationIdentity));
        if (string.IsNullOrWhiteSpace(ProtocolIdentity))
            throw new ArgumentException("Protocol identity must not be blank.", nameof(ProtocolIdentity));
        ArgumentNullException.ThrowIfNull(SamplingWindow);
        SamplingWindow.Validate();
    }

    private sealed record CanonicalWorkload(string Id, string Version, string ConfigurationIdentity,
        string ProtocolIdentity, int SampleCount, int InvocationsPerSample);
}

public sealed record CookingNetworkMeasurementEnvironment(
    string MachineName,
    string OsDescription,
    string ProcessArchitecture,
    string FrameworkDescription,
    int ProcessorCount,
    string BuildIdentity,
    string NicStatus,
    string NicIdentity);

public sealed record CookingNetworkMeasurementEndpoint(
    CookingNetworkMeasurementTopology Topology,
    CookingNetworkEndpointRole Role,
    string EndpointId,
    string AddressStatus,
    string FirewallStatus);

public sealed record CookingNetworkMeasurementOperation(
    int InvocationIndex,
    long IngressToCommitTimestampTicks,
    long ThreadAllocatedBytes,
    double IngressToCommitNanoseconds,
    double AllocatedBytes,
    int QueueDepthBefore,
    int MaximumQueueDepth,
    int AcceptedCount,
    int RejectedCount,
    int DuplicateCount,
    string BeforeStateHash,
    string AfterStateHash);

public sealed record CookingNetworkMeasurementSample(
    int Index,
    long Operations,
    long IngressToCommitTimestampTicks,
    long ThreadAllocatedBytes,
    double IngressToCommitNanosecondsPerOperation,
    double AllocatedBytesPerOperation,
    int QueueDepthBefore,
    int MaximumQueueDepth,
    int AcceptedCount,
    int RejectedCount,
    int DuplicateCount,
    string BeforeStateHash,
    string AfterStateHash,
    IReadOnlyList<CookingNetworkMeasurementOperation> OperationTrace);

public sealed record CookingNetworkMeasurementSummary(
    int SampleCount,
    long TotalOperations,
    double MeanIngressToCommitNanosecondsPerOperation,
    double P50IngressToCommitNanosecondsPerOperation,
    double P95IngressToCommitNanosecondsPerOperation,
    double P99IngressToCommitNanosecondsPerOperation,
    double OperationsPerSecond,
    double MeanAllocatedBytesPerOperation,
    int AcceptedCount,
    int RejectedCount,
    int DuplicateCount,
    int MaximumQueueDepth);

public sealed record CookingNetworkMeasurementReport
{
    public const string Schema = "abilitykit.cooking-network-measurement.v1";

    public required DateTimeOffset TimestampUtc { get; init; }
    public string SchemaVersion { get; init; } = Schema;
    public required string MeasurementMode { get; init; }
    public required CookingNetworkWorkload Workload { get; init; }
    public required string WorkloadSha256 { get; init; }
    public required CookingNetworkMeasurementEnvironment Environment { get; init; }
    public required CookingNetworkMeasurementEndpoint Endpoint { get; init; }
    public required CookingNetworkThresholdStatus ThresholdStatus { get; init; }
    public required string FaultProfile { get; init; }
    public required string ComparabilityStatus { get; init; }
    public required IReadOnlyDictionary<string, string> MetricDefinitions { get; init; }
    public required IReadOnlyList<CookingNetworkMeasurementSample> Samples { get; init; }
    public required CookingNetworkMeasurementSummary Summary { get; init; }
    public required IReadOnlyList<string> Notes { get; init; }
}

public sealed record CookingNetworkMeasurementInvocation(
    CookingSimulation Simulation,
    CookingSessionAuthority Authority,
    ConnectionId Connection,
    CookingCommand Command,
    string CorrelationId);

public interface ICookingNetworkMeasurementFixture
{
    CookingNetworkMeasurementInvocation CreateInvocation(CookingNetworkWorkload workload, int sampleIndex, int invocationIndex);
}

public static class CookingNetworkBaselineMeasurementRunner
{
    public static readonly IReadOnlyDictionary<string, string> MetricDefinitions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ingressToCommit"] = "Stopwatch time from CookingSessionAuthority.EnqueueCommand through ExecuteNextBatch for in-process authority only.",
            ["queueDepth"] = "Authority queue depth before ingress and peak depth during the measured logical operation.",
            ["throughput"] = "Logical authority commands per elapsed measurement interval; informational only.",
            ["threadAllocation"] = "GC.GetAllocatedBytesForCurrentThread delta around ingress-to-commit only; not process, native, NIC or GPU memory.",
            ["stateHash"] = "SHA-256 of the authoritative CookingSimulation snapshot before and after every measured logical operation.",
            ["threshold"] = "All P6 thresholds are UNSET. This report is diagnostic evidence, not a performance pass/fail gate."
        }.ToFrozenDictionary(StringComparer.Ordinal);

    public static CookingNetworkMeasurementReport Run(
        CookingNetworkWorkload workload,
        ICookingNetworkMeasurementFixture fixture,
        CookingNetworkMeasurementEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(workload);
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentNullException.ThrowIfNull(endpoint);
        workload.Validate();
        ValidateEndpoint(endpoint);

        var samples = new List<CookingNetworkMeasurementSample>(workload.SamplingWindow.SampleCount);
        for (var sampleIndex = 0; sampleIndex < workload.SamplingWindow.SampleCount; sampleIndex++)
        {
            long elapsedTicks = 0;
            long allocatedBytes = 0;
            var accepted = 0;
            var rejected = 0;
            var duplicates = 0;
            var queueDepthBefore = 0;
            var maximumQueueDepth = 0;
            string? beforeHash = null;
            string? afterHash = null;

            var operationTrace = new List<CookingNetworkMeasurementOperation>(workload.SamplingWindow.InvocationsPerSample);
            for (var invocationIndex = 0; invocationIndex < workload.SamplingWindow.InvocationsPerSample; invocationIndex++)
            {
                var invocation = fixture.CreateInvocation(workload, sampleIndex, invocationIndex)
                    ?? throw new InvalidOperationException("Measurement fixture returned no invocation.");
                ValidateInvocation(workload, invocation);
                var operationBeforeHash = invocation.Simulation.Snapshot().Sha256();
                beforeHash ??= operationBeforeHash;
                var operationQueueDepthBefore = invocation.Authority.QueueDepth;
                queueDepthBefore = Math.Max(queueDepthBefore, operationQueueDepthBefore);
                maximumQueueDepth = Math.Max(maximumQueueDepth, operationQueueDepthBefore);

                var allocationStart = GC.GetAllocatedBytesForCurrentThread();
                var timestampStart = Stopwatch.GetTimestamp();
                var ingress = invocation.Authority.EnqueueCommand(invocation.Connection, invocation.Command, invocation.CorrelationId);
                var executions = invocation.Authority.ExecuteNextBatch();
                var operationElapsedTicks = Stopwatch.GetTimestamp() - timestampStart;
                var operationAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
                elapsedTicks += operationElapsedTicks;
                allocatedBytes += operationAllocatedBytes;
                var operationMaximumQueueDepth = Math.Max(operationQueueDepthBefore,
                    Math.Max(ingress.QueueDepth, invocation.Authority.QueueDepth));
                maximumQueueDepth = Math.Max(maximumQueueDepth, operationMaximumQueueDepth);
                var operationAfterHash = invocation.Simulation.Snapshot().Sha256();
                afterHash = operationAfterHash;

                var operationDuplicates = ingress.Disposition == CookingSessionCommandDisposition.Duplicate ? 1 : 0;
                var operationRejected = ingress.Disposition is CookingSessionCommandDisposition.Rejected or CookingSessionCommandDisposition.Cancelled ? 1 : 0;
                var operationAccepted = executions.Count(result => result.AuthorityResult?.Outcome == CommandOutcome.Accepted);
                operationRejected += executions.Count(result => result.AuthorityResult?.Outcome == CommandOutcome.Rejected);
                operationDuplicates += executions.Count(result => result.IsDuplicate || result.AuthorityResult?.IsDuplicate == true);
                accepted += operationAccepted;
                rejected += operationRejected;
                duplicates += operationDuplicates;
                operationTrace.Add(new CookingNetworkMeasurementOperation(invocationIndex, operationElapsedTicks,
                    operationAllocatedBytes, TimestampTicksToNanoseconds(operationElapsedTicks), operationAllocatedBytes,
                    operationQueueDepthBefore, operationMaximumQueueDepth, operationAccepted, operationRejected,
                    operationDuplicates, operationBeforeHash, operationAfterHash));
            }

            var operations = workload.SamplingWindow.InvocationsPerSample;
            samples.Add(new CookingNetworkMeasurementSample(sampleIndex, operations, elapsedTicks, allocatedBytes,
                TimestampTicksToNanoseconds(elapsedTicks) / operations, allocatedBytes / (double)operations,
                queueDepthBefore, maximumQueueDepth, accepted, rejected, duplicates, beforeHash!, afterHash!,
                operationTrace.AsReadOnly()));
        }

        return new CookingNetworkMeasurementReport
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            MeasurementMode = "baseline-only",
            Workload = workload,
            WorkloadSha256 = workload.Sha256(),
            Environment = CaptureEnvironment(),
            Endpoint = endpoint,
            ThresholdStatus = CookingNetworkThresholdStatus.Unset,
            FaultProfile = "none",
            ComparabilityStatus = "in-process-only; compare only identical workload, configuration, protocol, sampling window and topology",
            MetricDefinitions = MetricDefinitions,
            Samples = samples.AsReadOnly(),
            Summary = Summarize(samples),
            Notes = Array.AsReadOnly(new[]
            {
                "This runner is transport-neutral and invokes one in-process CookingSessionAuthority per measured logical operation.",
                "It does not open a socket, select a transport, exercise TCP framing, or establish two-PC LAN evidence.",
                "Setup, fixture construction, report serialization and artifact writing are outside ingress-to-commit timing.",
                "Logical application-fault ticks are diagnostic scheduling metadata, not measured wall-clock network latency."
            })
        };
    }

    private static void ValidateInvocation(CookingNetworkWorkload workload, CookingNetworkMeasurementInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation.Simulation);
        ArgumentNullException.ThrowIfNull(invocation.Authority);
        ArgumentNullException.ThrowIfNull(invocation.Command);
        if (string.IsNullOrWhiteSpace(invocation.CorrelationId))
            throw new ArgumentException("Measurement correlation ID must not be blank.", nameof(invocation));
        var descriptor = invocation.Authority.Descriptor;
        if (!StringComparer.Ordinal.Equals(descriptor.ConfigIdentity, workload.ConfigurationIdentity))
            throw new ArgumentException("Measurement workload configuration identity must match the authority descriptor.", nameof(invocation));
        if (!StringComparer.Ordinal.Equals(ProtocolIdentity(descriptor.Protocol), workload.ProtocolIdentity))
            throw new ArgumentException("Measurement workload protocol identity must match the authority descriptor.", nameof(invocation));
    }

    private static void ValidateEndpoint(CookingNetworkMeasurementEndpoint endpoint)
    {
        if (endpoint.Topology != CookingNetworkMeasurementTopology.InProcess)
            throw new ArgumentOutOfRangeException(nameof(endpoint), "The current baseline runner only supports explicitly labelled in-process topology.");
        if (endpoint.Role != CookingNetworkEndpointRole.Host)
            throw new ArgumentOutOfRangeException(nameof(endpoint), "The baseline runner measures a host-owned authority and must be labelled Host.");
        if (string.IsNullOrWhiteSpace(endpoint.EndpointId))
            throw new ArgumentException("Endpoint ID must not be blank.", nameof(endpoint));
        if (string.IsNullOrWhiteSpace(endpoint.AddressStatus) || string.IsNullOrWhiteSpace(endpoint.FirewallStatus))
            throw new ArgumentException("In-process endpoint address and firewall status must be explicit.", nameof(endpoint));
    }

    private static CookingNetworkMeasurementEnvironment CaptureEnvironment() => new(
        Environment.MachineName,
        RuntimeInformation.OSDescription,
        RuntimeInformation.ProcessArchitecture.ToString(),
        RuntimeInformation.FrameworkDescription,
        Environment.ProcessorCount,
        BuildIdentity(),
        "not-applicable",
        "not-applicable-in-process");

    private static string BuildIdentity()
    {
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        var commit = Environment.GetEnvironmentVariable("GITHUB_SHA")
            ?? Environment.GetEnvironmentVariable("BUILD_SOURCEVERSION")
            ?? "unknown";
        return $"{configuration}:{commit}";
    }

    private static CookingNetworkMeasurementSummary Summarize(IReadOnlyList<CookingNetworkMeasurementSample> samples)
    {
        var normalized = samples.Select(sample => sample.IngressToCommitNanosecondsPerOperation).OrderBy(value => value).ToArray();
        var totalOperations = samples.Sum(sample => sample.Operations);
        var totalTicks = samples.Sum(sample => sample.IngressToCommitTimestampTicks);
        var elapsedSeconds = totalTicks / (double)Stopwatch.Frequency;
        return new CookingNetworkMeasurementSummary(samples.Count, totalOperations,
            TimestampTicksToNanoseconds(totalTicks) / totalOperations,
            Percentile(normalized, 0.50), Percentile(normalized, 0.95), Percentile(normalized, 0.99),
            elapsedSeconds > 0 ? totalOperations / elapsedSeconds : 0d,
            samples.Sum(sample => sample.ThreadAllocatedBytes) / (double)totalOperations,
            samples.Sum(sample => sample.AcceptedCount), samples.Sum(sample => sample.RejectedCount),
            samples.Sum(sample => sample.DuplicateCount), samples.Max(sample => sample.MaximumQueueDepth));
    }

    private static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
    {
        var index = Math.Clamp((int)Math.Ceiling(percentile * sortedValues.Count) - 1, 0, sortedValues.Count - 1);
        return sortedValues[index];
    }

    private static double TimestampTicksToNanoseconds(long ticks) => ticks * 1_000_000_000d / Stopwatch.Frequency;

    public static string ProtocolIdentity(CookingProtocolIdentity protocol) =>
        $"{protocol.Name}:{protocol.MinimumVersion}-{protocol.MaximumVersion}";
}

public sealed record CookingApplicationFaultProfile(
    int DelayTicks = 0,
    int JitterTicks = 0,
    int DropEveryNthMessage = 0,
    int DuplicateEveryNthMessage = 0,
    bool Reorder = false,
    int? DisconnectAfterDeliveries = null)
{
    public void Validate()
    {
        if (DelayTicks < 0 || JitterTicks < 0 || DropEveryNthMessage < 0 || DuplicateEveryNthMessage < 0)
            throw new ArgumentOutOfRangeException(nameof(DelayTicks), "Fault profile values must be non-negative.");
        if (DisconnectAfterDeliveries is <= 0)
            throw new ArgumentOutOfRangeException(nameof(DisconnectAfterDeliveries), "Disconnect delivery count must be positive when specified.");
    }
}

public enum CookingNetworkFaultDisposition
{
    Delivered,
    Dropped,
    NotDeliveredAfterDisconnect,
}

public sealed record CookingNetworkFaultTrace(
    int MessageOrdinal,
    int DeliveryOrdinal,
    long LogicalDeliveryTick,
    CookingNetworkFaultDisposition Disposition,
    string Faults,
    long SnapshotSequence,
    CookingSessionReason Reason,
    CookingSynchronizationState SynchronizationState);

public sealed record CookingNetworkFaultRunResult(
    CookingApplicationFaultProfile Profile,
    IReadOnlyList<CookingNetworkFaultTrace> Trace,
    int DeliveredCount,
    int DroppedCount,
    int DuplicateCount,
    bool Disconnected,
    CookingSynchronizationState FinalSynchronizationState,
    string AuthorityStateHash);

public static class CookingNetworkFaultMeasurementRunner
{
    public static CookingNetworkFaultRunResult ApplyDeltas(
        CookingSessionAuthority authority,
        ConnectionId connection,
        CookingSnapshotBaseline baseline,
        IEnumerable<CookingSnapshotDelta> deltas,
        CookingApplicationFaultProfile profile,
        string correlationPrefix)
    {
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(deltas);
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrWhiteSpace(correlationPrefix))
            throw new ArgumentException("Correlation prefix must not be blank.", nameof(correlationPrefix));
        profile.Validate();

        var baselineResult = authority.InstallBaseline(connection, baseline, $"{correlationPrefix}-baseline");
        if (!baselineResult.Accepted)
            throw new InvalidOperationException($"Fault measurement baseline was rejected: {baselineResult.Reason}.");

        var scheduled = Schedule(deltas.ToArray(), profile);
        var trace = new List<CookingNetworkFaultTrace>();
        var delivered = 0;
        var dropped = 0;
        var duplicates = 0;
        var disconnected = false;
        var state = baselineResult.State;

        foreach (var scheduledDelta in scheduled)
        {
            if (disconnected)
            {
                trace.Add(new CookingNetworkFaultTrace(scheduledDelta.MessageOrdinal, 0, scheduledDelta.LogicalDeliveryTick,
                    CookingNetworkFaultDisposition.NotDeliveredAfterDisconnect, scheduledDelta.Faults,
                    scheduledDelta.Delta.SnapshotSequence, CookingSessionReason.ConnectionClosed, state));
                continue;
            }
            if (scheduledDelta.Disposition == CookingNetworkFaultDisposition.Dropped)
            {
                dropped++;
                trace.Add(new CookingNetworkFaultTrace(scheduledDelta.MessageOrdinal, 0, scheduledDelta.LogicalDeliveryTick,
                    scheduledDelta.Disposition, scheduledDelta.Faults, scheduledDelta.Delta.SnapshotSequence,
                    CookingSessionReason.None, state));
                continue;
            }

            delivered++;
            if (scheduledDelta.IsDuplicate)
                duplicates++;
            var result = authority.ApplyDelta(connection, scheduledDelta.Delta,
                $"{correlationPrefix}-delivery-{delivered}");
            state = result.State;
            trace.Add(new CookingNetworkFaultTrace(scheduledDelta.MessageOrdinal, delivered, scheduledDelta.LogicalDeliveryTick,
                CookingNetworkFaultDisposition.Delivered, scheduledDelta.Faults, scheduledDelta.Delta.SnapshotSequence,
                result.Reason, result.State));
            if (profile.DisconnectAfterDeliveries == delivered)
            {
                var loss = authority.ReportTransportLoss(connection, $"{correlationPrefix}-disconnect");
                disconnected = true;
                state = loss.State;
            }
        }

        return new CookingNetworkFaultRunResult(profile, trace.AsReadOnly(), delivered, dropped, duplicates, disconnected, state,
            baseline.SnapshotHash);
    }

    private static IReadOnlyList<ScheduledDelta> Schedule(IReadOnlyList<CookingSnapshotDelta> deltas, CookingApplicationFaultProfile profile)
    {
        var scheduled = new List<ScheduledDelta>();
        for (var index = 0; index < deltas.Count; index++)
        {
            var ordinal = index + 1;
            var dropped = profile.DropEveryNthMessage > 0 && ordinal % profile.DropEveryNthMessage == 0;
            var jitter = profile.JitterTicks == 0 ? 0 : index % (profile.JitterTicks + 1);
            var tick = profile.DelayTicks + jitter + ordinal;
            var faults = new List<string>();
            if (profile.DelayTicks > 0)
                faults.Add($"delay:{profile.DelayTicks}");
            if (jitter > 0)
                faults.Add($"jitter:{jitter}");
            if (dropped)
                faults.Add("loss");
            scheduled.Add(new ScheduledDelta(ordinal, deltas[index], tick,
                dropped ? CookingNetworkFaultDisposition.Dropped : CookingNetworkFaultDisposition.Delivered,
                false, string.Join(',', faults)));
            if (!dropped && profile.DuplicateEveryNthMessage > 0 && ordinal % profile.DuplicateEveryNthMessage == 0)
            {
                faults.Add("duplicate");
                scheduled.Add(new ScheduledDelta(ordinal, deltas[index], tick, CookingNetworkFaultDisposition.Delivered,
                    true, string.Join(',', faults)));
            }
        }

        var ordered = scheduled.OrderBy(item => item.LogicalDeliveryTick).ThenBy(item => item.MessageOrdinal).ToArray();
        if (!profile.Reorder)
            return ordered;
        return ordered.Reverse()
            .Select(item => item with { Faults = AppendFault(item.Faults, "reorder") }).ToArray();
    }

    private static string AppendFault(string faults, string fault) => string.IsNullOrWhiteSpace(faults) ? fault : $"{faults},{fault}";

    private sealed record ScheduledDelta(int MessageOrdinal, CookingSnapshotDelta Delta, long LogicalDeliveryTick,
        CookingNetworkFaultDisposition Disposition, bool IsDuplicate, string Faults);
}

public sealed record CookingNetworkOptimizationGateResult(
    CookingNetworkOptimizationStrategy Strategy,
    CookingNetworkOptimizationGateStatus Status,
    CookingNetworkOptimizationBlockReason Reason,
    string Message);

public sealed record CookingNetworkOptimizationPrerequisites(
    CookingNetworkThresholdStatus ThresholdStatus,
    bool HasOwnerApproval,
    bool HasRealLanEvidence,
    bool HasFallbackPolicy);

public static class CookingNetworkOptimizationGate
{
    public static CookingNetworkOptimizationGateResult Evaluate(
        CookingNetworkOptimizationStrategy strategy,
        CookingNetworkOptimizationPrerequisites prerequisites)
    {
        ArgumentNullException.ThrowIfNull(prerequisites);
        if (strategy == CookingNetworkOptimizationStrategy.Baseline)
            return new CookingNetworkOptimizationGateResult(strategy, CookingNetworkOptimizationGateStatus.BaselineOnly,
                CookingNetworkOptimizationBlockReason.None, "Baseline diagnostics are permitted; no optimization is enabled.");
        if (prerequisites.ThresholdStatus == CookingNetworkThresholdStatus.Unset)
            return Block(strategy, CookingNetworkOptimizationBlockReason.ThresholdsUnset,
                "Optimization remains blocked because performance thresholds are UNSET.");
        if (!prerequisites.HasOwnerApproval)
            return Block(strategy, CookingNetworkOptimizationBlockReason.OwnerApprovalMissing,
                "Optimization remains blocked because owner approval is missing.");
        if (!prerequisites.HasRealLanEvidence)
            return Block(strategy, CookingNetworkOptimizationBlockReason.RealLanEvidenceMissing,
                "Optimization remains blocked because real two-PC LAN evidence is missing.");
        if (!prerequisites.HasFallbackPolicy)
            return Block(strategy, CookingNetworkOptimizationBlockReason.FallbackPolicyMissing,
                "Optimization remains blocked because an approved fallback policy is missing.");
        return Block(strategy, CookingNetworkOptimizationBlockReason.OptimizationRuntimeNotImplemented,
            "Optimization remains blocked because interpolation, prediction and correction runtimes are not implemented.");
    }

    private static CookingNetworkOptimizationGateResult Block(
        CookingNetworkOptimizationStrategy strategy,
        CookingNetworkOptimizationBlockReason reason,
        string message) => new(strategy, CookingNetworkOptimizationGateStatus.Blocked, reason, message);
}

public sealed record CookingNetworkMeasurementEvidence(
    string TestId,
    string Operation,
    string WorkloadSha256,
    string Topology,
    string FaultProfile,
    string Result,
    string BeforeStateHash,
    string AfterStateHash,
    string AssertionSummary,
    string Runner,
    string TimestampUtc);

public static class CookingNetworkMeasurementArtifactWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private static readonly JsonSerializerOptions JsonLineOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static void WriteReport(string directory, CookingNetworkMeasurementReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "baseline-report.json"), JsonSerializer.Serialize(report, JsonOptions));
        File.WriteAllText(Path.Combine(directory, "baseline-summary.csv"), ToCsv(report));
    }

    public static void AppendEvidence(string path, CookingNetworkMeasurementEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new ArgumentException("Evidence path has no directory.", nameof(path)));
        File.AppendAllText(path, JsonSerializer.Serialize(evidence, JsonLineOptions) + Environment.NewLine, Encoding.UTF8);
    }

    public static IReadOnlyList<CookingNetworkMeasurementEvidence> ReadEvidence(string path) =>
        File.ReadLines(path).Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<CookingNetworkMeasurementEvidence>(line, JsonLineOptions)
                ?? throw new InvalidDataException("Invalid cooking network measurement evidence line."))
            .ToArray();

    private static string ToCsv(CookingNetworkMeasurementReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("sampleIndex,operations,ingressToCommitNanosecondsPerOperation,allocatedBytesPerOperation,queueDepthBefore,maximumQueueDepth,acceptedCount,rejectedCount,duplicateCount,beforeStateHash,afterStateHash");
        foreach (var sample in report.Samples)
        {
            builder.Append(sample.Index).Append(',').Append(sample.Operations).Append(',')
                .Append(sample.IngressToCommitNanosecondsPerOperation.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(sample.AllocatedBytesPerOperation.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(sample.QueueDepthBefore).Append(',').Append(sample.MaximumQueueDepth).Append(',')
                .Append(sample.AcceptedCount).Append(',').Append(sample.RejectedCount).Append(',').Append(sample.DuplicateCount)
                .Append(',').Append(sample.BeforeStateHash).Append(',').Append(sample.AfterStateHash).AppendLine();
        }
        return builder.ToString();
    }
}
