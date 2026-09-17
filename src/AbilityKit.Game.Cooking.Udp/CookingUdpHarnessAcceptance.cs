using System.Text;
using System.Text.Json;

namespace AbilityKit.Game.Cooking.Udp;

/// <summary>
/// Owns the versioned, role-scoped harness JSONL contract and its streaming acceptance reducer.
/// Role processes append only their own log; this reader retains only correlation and snapshot state.
/// </summary>
public static class CookingUdpHarnessAcceptance
{
    public const string EventSchema = "abilitykit.cooking-harness-event.v1";
    public const string ResultSchema = "abilitykit.cooking-harness-result.v1";
    public const string Topology = "udp-same-machine";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    public static async Task AppendEventAsync(string path, CookingHarnessEvent value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(value);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new ArgumentException("Event path has no directory.", nameof(path)));
        await File.AppendAllTextAsync(path, JsonSerializer.Serialize(value, JsonOptions) + Environment.NewLine,
            new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Validates only the specified run root. JSONL is read once per role and each event is immediately reduced.
    /// </summary>
    public static CookingHarnessAcceptanceResult Validate(string runRoot, string expectedRunId, CookingSessionDescriptor descriptor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedRunId);
        ArgumentNullException.ThrowIfNull(descriptor);

        var root = Path.GetFullPath(runRoot);
        var hostDirectory = Path.GetFullPath(Path.Combine(root, "host"));
        var clientDirectory = Path.GetFullPath(Path.Combine(root, "client"));
        ValidateRoleDirectories(root, hostDirectory, clientDirectory);

        var hostResult = ReadRoleResult(hostDirectory, "host", expectedRunId, descriptor);
        var clientResult = ReadRoleResult(clientDirectory, "client", expectedRunId, descriptor);
        if (hostResult.AuthorityDispatcherOnly != true)
            throw new InvalidDataException("Host authority ran in UDP callback context.");
        if (!hostResult.OrderAccepted || !clientResult.OrderAccepted)
            throw new InvalidDataException("Fixture order was not accepted by both role projections.");
        if (!StringComparer.Ordinal.Equals(hostResult.StateHash, clientResult.StateHash))
            throw new InvalidDataException("Host and client hashes do not converge.");
        if (!StringComparer.Ordinal.Equals(hostResult.OrderId, clientResult.OrderId))
            throw new InvalidDataException("Host and client completed different orders.");
        ValidateSettlement(hostResult);
        ValidateSettlement(clientResult);

        var host = ReduceRoleLog(Path.Combine(hostDirectory, "events.jsonl"), "host", expectedRunId, descriptor);
        var client = ReduceRoleLog(Path.Combine(clientDirectory, "events.jsonl"), "client", expectedRunId, descriptor);
        host.ValidateHost(hostResult);
        client.ValidateClient(clientResult);

        return new CookingHarnessAcceptanceResult(expectedRunId, host.EventCount, client.EventCount, hostResult.StateHash,
            hostResult.OrderId, host.SnapshotSequence, client.SnapshotSequence, client.BaselineReference);
    }

    private static void ValidateRoleDirectories(string root, string host, string client)
    {
        if (!IsDirectChild(root, host) || !IsDirectChild(root, client) || PathEquals(host, client) || IsNested(host, client) || IsNested(client, host))
            throw new InvalidDataException("Role artifact directories must be distinct direct children of the run root.");
    }

    private static bool IsDirectChild(string root, string path) =>
        PathEquals(Path.GetDirectoryName(path) ?? string.Empty, root);

    private static bool IsNested(string parent, string child) =>
        child.StartsWith(parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);

    private static bool PathEquals(string left, string right) =>
        StringComparer.OrdinalIgnoreCase.Equals(Path.GetFullPath(left), Path.GetFullPath(right));

    private static CookingHarnessRoleResult ReadRoleResult(string roleDirectory, string expectedRole, string expectedRunId,
        CookingSessionDescriptor descriptor)
    {
        var failure = Path.Combine(roleDirectory, "failure.json");
        if (File.Exists(failure))
            throw new InvalidDataException($"Role failure document exists: {failure}");
        var path = Path.Combine(roleDirectory, "result.json");
        if (!File.Exists(path))
            throw new InvalidDataException($"Missing role result: {path}");

        CookingHarnessRoleResult? result;
        try
        {
            result = JsonSerializer.Deserialize<CookingHarnessRoleResult>(File.ReadAllText(path), JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Invalid role result: {path}", exception);
        }

        if (result is null || !StringComparer.Ordinal.Equals(result.Schema, ResultSchema) ||
            !StringComparer.Ordinal.Equals(result.RunId, expectedRunId) || !StringComparer.Ordinal.Equals(result.Role, expectedRole) ||
            !StringComparer.Ordinal.Equals(result.Topology, Topology) || string.IsNullOrWhiteSpace(result.FixtureId) ||
            string.IsNullOrWhiteSpace(result.ConfigIdentity) || !StringComparer.Ordinal.Equals(result.ConfigIdentity, descriptor.ConfigIdentity) ||
            string.IsNullOrWhiteSpace(result.StateHash) || string.IsNullOrWhiteSpace(result.OrderId) || result.Scope is null ||
            !Equals(result.Scope.ToDomain(), descriptor.Scope) || result.Epoch != descriptor.Epoch)
        {
            throw new InvalidDataException($"Role result has an invalid typed contract: {path}");
        }

        return result;
    }

    private static void ValidateSettlement(CookingHarnessRoleResult result)
    {
        if (result.Settlement.ValueKind != JsonValueKind.Object ||
            !result.Settlement.TryGetProperty("orderId", out var order) ||
            !StringComparer.Ordinal.Equals(order.GetString(), result.OrderId) ||
            !result.Settlement.TryGetProperty("completion", out var completion) ||
            !StringComparer.Ordinal.Equals(completion.GetString(), "fixture-completed"))
        {
            throw new InvalidDataException($"Role result has no valid fixture settlement: {result.Role}.");
        }
    }

    private static RoleLogReducer ReduceRoleLog(string path, string expectedRole, string expectedRunId,
        CookingSessionDescriptor descriptor)
    {
        if (!File.Exists(path))
            throw new InvalidDataException($"Missing required event log: {path}");

        var reducer = new RoleLogReducer(path, expectedRole, expectedRunId, descriptor);
        try
        {
            foreach (var line in File.ReadLines(path, Encoding.UTF8))
                reducer.Observe(line);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Invalid JSONL event in {path}.", exception);
        }
        reducer.Complete();
        return reducer;
    }

    public sealed record CookingHarnessEvent(
        string Schema,
        string RunId,
        string Role,
        long Sequence,
        string TimestampUtc,
        string EventType,
        CookingUdpScope Scope,
        long Epoch,
        string CorrelationId,
        string? CommandId,
        string? OrderId,
        string Outcome,
        string Reason,
        string? StateHash,
        long? SnapshotSequence,
        long? BaselineReference);

    public sealed record CookingHarnessRoleResult(
        string Schema,
        string RunId,
        string Role,
        string Topology,
        string FixtureId,
        string ConfigIdentity,
        CookingUdpScope Scope,
        long Epoch,
        string StateHash,
        string OrderId,
        bool OrderAccepted,
        JsonElement Settlement,
        bool? AuthorityDispatcherOnly = null);

    public sealed record CookingHarnessAcceptanceResult(string RunId, int HostEventCount, int ClientEventCount,
        string StateHash, string OrderId, long? HostSnapshotSequence, long? ClientSnapshotSequence, long? BaselineReference);

    private sealed class RoleLogReducer
    {
        private static readonly HashSet<string> KnownEventTypes = new(StringComparer.Ordinal)
        {
            "host-started", "host-ready", "baseline-installed", "gameplay-command", "command-result", "snapshot-delta", "role-result", "role-failure",
        };

        private readonly string _path;
        private readonly string _role;
        private readonly string _runId;
        private readonly CookingSessionDescriptor _descriptor;
        private readonly Dictionary<string, CommandIdentity> _commands = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _submittedByCorrelation = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _resultsByCorrelation = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _deltasByCorrelation = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _stateMutationsByCorrelation = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _acceptedResultsByCommand = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _rejectedResultsByCommand = new(StringComparer.Ordinal);
        private long _previousEventSequence;
        private long? _baselineReference;
        private long? _snapshotSequence;
        private bool _roleResultAccepted;
        private string? _roleResultHash;
        private int _eventCount;

        public RoleLogReducer(string path, string role, string runId, CookingSessionDescriptor descriptor)
        {
            _path = path;
            _role = role;
            _runId = runId;
            _descriptor = descriptor;
        }

        public int EventCount => _eventCount;
        public long? BaselineReference => _baselineReference;
        public long? SnapshotSequence => _snapshotSequence;

        public void Observe(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                throw new InvalidDataException($"Blank JSONL event line in {_path}.");
            CookingHarnessEvent? value = JsonSerializer.Deserialize<CookingHarnessEvent>(line, JsonOptions);
            if (value is null || !StringComparer.Ordinal.Equals(value.Schema, EventSchema) ||
                !StringComparer.Ordinal.Equals(value.RunId, _runId) || !StringComparer.Ordinal.Equals(value.Role, _role) ||
                value.Sequence != _previousEventSequence + 1 || !KnownEventTypes.Contains(value.EventType) ||
                value.Scope is null || !Equals(value.Scope.ToDomain(), _descriptor.Scope) || value.Epoch != _descriptor.Epoch ||
                string.IsNullOrWhiteSpace(value.CorrelationId) || string.IsNullOrWhiteSpace(value.Outcome) ||
                string.IsNullOrWhiteSpace(value.Reason) || !DateTimeOffset.TryParse(value.TimestampUtc, out _))
            {
                throw new InvalidDataException($"Invalid typed event contract in {_path}.");
            }

            _previousEventSequence = value.Sequence;
            _eventCount++;
            switch (value.EventType)
            {
                case "role-failure":
                    throw new InvalidDataException($"Role failure event exists in {_path}.");
                case "gameplay-command":
                    ObserveCommand(value);
                    break;
                case "command-result":
                    ObserveCommandResult(value);
                    break;
                case "baseline-installed":
                    ObserveBaseline(value);
                    break;
                case "snapshot-delta":
                    ObserveDelta(value);
                    break;
                case "role-result":
                    if (!StringComparer.Ordinal.Equals(value.Outcome, "accepted") || string.IsNullOrWhiteSpace(value.StateHash))
                        throw new InvalidDataException($"Role result event is not accepted in {_path}.");
                    _roleResultAccepted = true;
                    _roleResultHash = value.StateHash;
                    break;
            }
        }

        public void Complete()
        {
            if (_eventCount == 0 || !_roleResultAccepted)
                throw new InvalidDataException($"Required completion event is missing in {_path}.");
            foreach (var (correlation, submitted) in _submittedByCorrelation)
            {
                if (!_resultsByCorrelation.TryGetValue(correlation, out var results) || results < submitted)
                    throw new InvalidDataException($"Command/result correlation is incomplete in {_path}: {correlation}.");
            }
            foreach (var (correlation, mutations) in _stateMutationsByCorrelation)
            {
                if (mutations != 1 || !_deltasByCorrelation.TryGetValue(correlation, out var deltas) || deltas != 1)
                    throw new InvalidDataException($"Accepted mutation has no single correlated delta in {_path}: {correlation}.");
            }
        }

        public void ValidateHost(CookingHarnessRoleResult result)
        {
            RequireAcceptedCommand("host-pickup");
            RequireAcceptedCommand("host-start");
            RequireAcceptedCommand("host-tick-1");
            RequireAcceptedCommand("host-tick-2");
            RequireAcceptedCommand("host-tick-3");
            if (!StringComparer.Ordinal.Equals(_roleResultHash, result.StateHash))
                throw new InvalidDataException("Host role-result hash differs from its result document.");
        }

        public void ValidateClient(CookingHarnessRoleResult result)
        {
            RequireAcceptedCommand("remote-plate");
            RequireRejectedCommand("remote-reject");
            if (!TryFindCommand("remote-reject", out var rejection) ||
                !StringComparer.Ordinal.Equals(rejection.OrderId, "fixture-order-rejected"))
            {
                throw new InvalidDataException("Rejected order command is missing its order identity.");
            }
            if (!TryFindCommand("remote-submit", out var submission, out var submissionCorrelation) ||
                !StringComparer.Ordinal.Equals(submission.OrderId, result.OrderId) ||
                !_acceptedResultsByCommand.TryGetValue("remote-submit", out var accepted) || accepted != 2 ||
                !_resultsByCorrelation.TryGetValue(submissionCorrelation, out var submitResults) || submitResults != 2)
            {
                throw new InvalidDataException("Submit replay did not retain one initial acceptance and one duplicate result.");
            }
            if (_baselineReference is null || _snapshotSequence is null || !StringComparer.Ordinal.Equals(_roleResultHash, result.StateHash))
                throw new InvalidDataException("Client baseline, delta, or final hash evidence is incomplete.");
        }

        private void ObserveCommand(CookingHarnessEvent value)
        {
            if (!StringComparer.Ordinal.Equals(value.Outcome, "submitted") || string.IsNullOrWhiteSpace(value.CommandId))
                throw new InvalidDataException($"Invalid gameplay command event in {_path}.");
            var identity = new CommandIdentity(value.CommandId, value.OrderId);
            if (_commands.TryGetValue(value.CorrelationId, out var previous) && !Equals(previous, identity))
                throw new InvalidDataException($"Correlation ID was reused for a different command in {_path}.");
            _commands[value.CorrelationId] = identity;
            _submittedByCorrelation[value.CorrelationId] = _submittedByCorrelation.GetValueOrDefault(value.CorrelationId) + 1;
        }

        private void ObserveCommandResult(CookingHarnessEvent value)
        {
            if (!_commands.TryGetValue(value.CorrelationId, out var command) ||
                !StringComparer.Ordinal.Equals(command.CommandId, value.CommandId) ||
                !StringComparer.Ordinal.Equals(command.OrderId, value.OrderId) ||
                (value.Outcome is not "accepted" and not "rejected"))
            {
                throw new InvalidDataException($"Command result is not correlated to its command in {_path}.");
            }
            _resultsByCorrelation[value.CorrelationId] = _resultsByCorrelation.GetValueOrDefault(value.CorrelationId) + 1;
            if (StringComparer.Ordinal.Equals(value.Outcome, "accepted") && _role == "client" &&
                !StringComparer.Ordinal.Equals(value.Reason, "Duplicate"))
            {
                _stateMutationsByCorrelation[value.CorrelationId] =
                    _stateMutationsByCorrelation.GetValueOrDefault(value.CorrelationId) + 1;
            }
            var target = StringComparer.Ordinal.Equals(value.Outcome, "accepted") ? _acceptedResultsByCommand : _rejectedResultsByCommand;
            target[command.CommandId] = target.GetValueOrDefault(command.CommandId) + 1;
        }

        private void ObserveBaseline(CookingHarnessEvent value)
        {
            if (!StringComparer.Ordinal.Equals(value.Outcome, "accepted") || value.SnapshotSequence is not > 0 ||
                value.BaselineReference != value.SnapshotSequence || _baselineReference is not null || string.IsNullOrWhiteSpace(value.StateHash))
            {
                throw new InvalidDataException($"Invalid baseline event in {_path}.");
            }
            _baselineReference = value.BaselineReference;
            _snapshotSequence = value.SnapshotSequence;
        }

        private void ObserveDelta(CookingHarnessEvent value)
        {
            if (!StringComparer.Ordinal.Equals(value.Outcome, "accepted") || _baselineReference is null || _snapshotSequence is null ||
                value.SnapshotSequence != _snapshotSequence + 1 || value.BaselineReference != _baselineReference ||
                string.IsNullOrWhiteSpace(value.StateHash))
            {
                throw new InvalidDataException($"Invalid baseline or non-continuous delta event in {_path}.");
            }
            _snapshotSequence = value.SnapshotSequence;
            _deltasByCorrelation[value.CorrelationId] = _deltasByCorrelation.GetValueOrDefault(value.CorrelationId) + 1;
        }

        private bool TryFindCommand(string commandId, out CommandIdentity command) =>
            TryFindCommand(commandId, out command, out _);

        private bool TryFindCommand(string commandId, out CommandIdentity command, out string correlation)
        {
            foreach (var pair in _commands)
            {
                if (StringComparer.Ordinal.Equals(pair.Value.CommandId, commandId))
                {
                    command = pair.Value;
                    correlation = pair.Key;
                    return true;
                }
            }
            command = default!;
            correlation = string.Empty;
            return false;
        }

        private void RequireAcceptedCommand(string commandId)
        {
            if (!_acceptedResultsByCommand.TryGetValue(commandId, out var count) || count != 1)
                throw new InvalidDataException($"Required accepted command result is missing: {commandId}.");
        }

        private void RequireRejectedCommand(string commandId)
        {
            if (!_rejectedResultsByCommand.TryGetValue(commandId, out var count) || count != 1)
                throw new InvalidDataException($"Required rejected command result is missing: {commandId}.");
        }

        private sealed record CommandIdentity(string CommandId, string? OrderId);
    }
}
