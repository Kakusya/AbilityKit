using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace AbilityKit.Game.Cooking.FlowAcceptance;

public sealed class FixedFlowSessionFactory : IFlowSessionFactory
{
    public IFlowSession Create(FlowMode mode) => mode switch
    {
        FlowMode.Offline => new OfflineFlowAdapter(),
        FlowMode.Network => new NetworkFlowAdapter(),
        _ => throw new InvalidDataException("Unknown flow mode")
    };
}

public sealed class NetworkFlowAdapter : IFlowSession
{
    private sealed class Role(string id, Process process)
    {
        internal string Id = id;
        internal Process Process = process;
        internal readonly int ProcessId = process.Id;
        internal readonly List<string> CleanupErrors = new();
        internal long ControlSequence, ReplySequence, EventSequence;
        internal readonly Channel<RoleReply> Replies = Channel.CreateBounded<RoleReply>(256);
        internal readonly Dictionary<string, RoleReply> Controls = new(StringComparer.Ordinal);
        internal readonly List<RoleControl> Sent = new();
        internal BindingFence? Binding;
        internal FlowObservation? Last;
        internal Task Reader = Task.CompletedTask, Diagnostics = Task.CompletedTask;
        internal string Stderr = "";
        internal string? Error, Endpoint;
        internal string? Identity;
        internal bool Stopped;
        internal int? ExitCode;
    }
    private readonly int owner = Environment.CurrentManagedThreadId;
    private readonly List<Role> roles = new();
    private readonly Dictionary<string, (Role Role, CookingRecipeCommand Command, BindingFence Binding)> calls = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CommandObservation> terminals = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource readers = new();
    private RunIdentity run = null!;
    private FlowRequest request = null!;
    private IFlowEventSink events = null!;
    private bool started, closing;
    private CleanupResult? cleanup;
    private string? error;
    private long controls;
    // Instance-local OS call seams; private tests can exercise exit races without a product/CLI fault hook.
    private Func<Process, bool> hasExited = static process => process.HasExited;
    private Action<Process> terminate = static process => process.Kill();
    private Func<Process, int> readExitCode = static process => process.ExitCode;

    public async ValueTask<FlowObservation> StartAsync(FlowRequest request, RunIdentity run, IFlowEventSink events, CancellationToken cancellationToken)
    {
        CheckOwner(); cancellationToken.ThrowIfCancellationRequested();
        if (started || closing) throw new InvalidOperationException("NetworkStartOnce");
        started = true; this.request = request; this.run = run; this.events = events;
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(request.Budgets.StartupMs);
        var server = Launch("server");
        var initialize = await Send(server, RoleControlKind.Initialize, bounded.Token,
            init: new("server", null, null, request.Fixture, request.Budgets, request.Logs));
        var serverReady = await Receive(server, r => r.Kind == RoleReplyKind.Ready && r.ControlId == initialize, bounded.Token);
        CheckActive(bounded.Token);
        var initial = serverReady.Event!.Observation!;
        if (!initial.Available || initial.Origin != ObservationOrigin.Authority || initial.ObserverId != "authority" ||
            initial.Fence.RunGeneration != run.RunGeneration || initial.Fence.Scope.MatchScope.Session.Value != run.RunId ||
            string.IsNullOrWhiteSpace(initial.Fence.ServerSessionInstance) || initial.Fence.ConnectionGeneration is not null)
            throw new InvalidDataException("NetworkServerReadyBinding");
        server.Binding = initial.Fence; server.Endpoint = serverReady.BoundEndpoint;
        var clients = new[] { Launch("client-a"), Launch("client-b") };
        var ids = new List<string>();
        foreach (var client in clients)
            ids.Add(await Send(client, RoleControlKind.Initialize, bounded.Token,
                init: new(client.Id, client.Id == "client-a" ? "A" : "B", server.Endpoint, null, request.Budgets, request.Logs)));
        for (var index = 0; index < clients.Length; index++)
        {
            var client = clients[index];
            var ready = await Receive(client, r => r.Kind == RoleReplyKind.Ready && r.ControlId == ids[index], bounded.Token);
            CheckActive(bounded.Token);
            var cut = ready.Event!.Observation!;
            ValidateClient(cut, initial.Fence, client.Id, null);
            if (cut.SynchronizedObserved != true) throw new InvalidDataException("ClientNotJoinedReady");
            client.Binding = cut.Fence;
        }
        return initial;
    }

    public async ValueTask<DispatchResult> DispatchGroupAsync(string stepId, IReadOnlyList<FlowAction> actions,
        int timeoutMs, CancellationToken cancellationToken)
    {
        CheckActive(cancellationToken); FlowJson.RequireId(stepId);
        if (actions.Count is < 1 or > 2 || timeoutMs <= 0 || actions.Select(a => a.CallId).Distinct().Count() != actions.Count ||
            actions.Select(a => a.ActorId).Distinct().Count() != actions.Count) throw new ArgumentException("Invalid network action group");
        foreach (var action in actions)
        {
            FlowJson.RequireId(action.CallId); FlowJson.RequireId(action.BusinessId);
            if (calls.ContainsKey(action.CallId) || !request.Fixture.Actors.Contains(action.ActorId) ||
                !Enum.IsDefined(action.Verb) || action.ItemId != request.Fixture.ItemId || action.ExpectedItemVersion <= 0 ||
                (action.Verb == FlowVerb.Pickup ? action.StationSlot is not null : action.StationSlot != request.Fixture.DropSlot))
                throw new ArgumentException("Invalid network action");
            var role = Client(action.ActorId);
            calls.Add(action.CallId, (role, new(role.Binding!.Scope.MatchScope, 0, new(action.ActorId), new(action.BusinessId),
                action.Verb == FlowVerb.Pickup ? CookingRecipeOperation.Pickup : CookingRecipeOperation.Drop,
                Item: new ItemId(action.ItemId), Station: action.StationSlot is null ? null : new StationSlotId(action.StationSlot),
                ExpectedItemVersion: action.ExpectedItemVersion), role.Binding!));
        }
        return await Dispatch(stepId, actions.Select(a => (a.CallId, Action: (FlowAction?)a, Replay: (string?)null)).ToArray(), timeoutMs, cancellationToken);
    }

    public async ValueTask<DispatchResult> ReplayAsync(string stepId, string originalCallId, string newCallId,
        int timeoutMs, CancellationToken cancellationToken)
    {
        CheckActive(cancellationToken); FlowJson.RequireId(stepId); FlowJson.RequireId(newCallId);
        if (timeoutMs <= 0 || calls.ContainsKey(newCallId) || !calls.TryGetValue(originalCallId, out var original))
            throw new ArgumentException("Replay requires known call/new ID");
        if (original.Role.Binding != original.Binding) throw new InvalidDataException("ReplayBindingChanged");
        calls.Add(newCallId, original);
        return await Dispatch(stepId, new[] { (newCallId, Action: (FlowAction?)null, Replay: (string?)originalCallId) }, timeoutMs, cancellationToken);
    }

    private async Task<DispatchResult> Dispatch(string step, IReadOnlyList<(string CallId, FlowAction? Action, string? Replay)> actions,
        int timeoutMs, CancellationToken token)
    {
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(token);
        bounded.CancelAfter(timeoutMs);
        string? failure = null;
        try
        {
            // Every client is armed before any barrier is released. Arrival frames remain real network facts.
            foreach (var a in actions)
                await Send(calls[a.CallId].Role, RoleControlKind.ArmAction, bounded.Token, id: a.CallId, action: a.Action, replay: a.Replay, barrier: step);
            foreach (var a in actions)
                await Receive(calls[a.CallId].Role, r => r.Kind == RoleReplyKind.Armed && r.ControlId == a.CallId, bounded.Token);
            CheckActive(bounded.Token);
            foreach (var a in actions) await Send(calls[a.CallId].Role, RoleControlKind.ReleaseBarrier, bounded.Token, barrier: step);
            foreach (var a in actions)
                await Receive(calls[a.CallId].Role, r => r.Event?.Command?.CallId == a.CallId, bounded.Token);
            CheckActive(bounded.Token);
        }
        catch (Exception e)
        {
            failure = e is OperationCanceledException ? token.IsCancellationRequested ? "CancelledAfterSendOrArm" : "CommandTimeout" : e.Message;
            // Drain already received facts without pretending a sent command was undone.
            foreach (var role in roles)
                while (role.Replies.Reader.TryRead(out var reply))
                    try { Accept(role, reply, throwFault: false); } catch (Exception) { }
        }
        var outcomes = actions.Select(a => terminals.TryGetValue(a.CallId, out var terminal) ? terminal :
            new CommandObservation(step, a.CallId, calls[a.CallId].Command.Command.Value, calls[a.CallId].Command.Player.Value,
                calls[a.CallId].Command, null, null, null, null, null, null, null, CallCompletion.Unknown, failure ?? "MissingTerminal")).ToArray();
        return new(failure is null && outcomes.All(o => o.Completion == CallCompletion.Terminal && o.BusinessResult is not null), outcomes, failure);
    }

    public async ValueTask<FlowObservation> CaptureAsync(string observerId, CancellationToken cancellationToken)
    {
        CheckActive(cancellationToken, requireEvidence: false);
        var role = observerId == "authority" ? roles.Single(r => r.Id == "server") : roles.Single(r => r.Id == observerId);
        var id = await Send(role, RoleControlKind.Observe, cancellationToken, point: "capture");
        var reply = await Receive(role, r => r.ControlId == id && r.Event?.Kind == FlowEventKind.StateObserved, cancellationToken);
        CheckActive(cancellationToken, requireEvidence: false);
        return reply.Event!.Observation!;
    }

    public async ValueTask<ConvergenceResult> WaitForProjectionAsync(ProjectionFence expected, int timeoutMs, CancellationToken cancellationToken)
    {
        CheckActive(cancellationToken);
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(Math.Min(timeoutMs, request.Budgets.ConvergenceMs));
        var last = new Dictionary<string, FlowObservation>(StringComparer.Ordinal);
        try
        {
            while (true)
            {
                foreach (var clientId in expected.RequiredClients)
                {
                    var cut = await CaptureAsync(clientId, bounded.Token);
                    last[clientId] = cut;
                    CheckActive(bounded.Token);
                }
                if (expected.RequiredClients.All(id => ProjectionMatches(last[id], expected))) return new(true, last.Values.ToArray(), null);
                await Task.Delay(10, bounded.Token);
                CheckActive(bounded.Token);
            }
        }
        catch (OperationCanceledException)
        { return new(false, last.Values.ToArray(), cancellationToken.IsCancellationRequested ? "ProjectionCancelled" : "ProjectionTimeout"); }
    }

    internal static bool ProjectionMatches(FlowObservation observation, ProjectionFence fence) =>
        observation.Available && observation.SynchronizedObserved == true && observation.Origin == ObservationOrigin.Client && fence.RequiredClients.Contains(observation.ObserverId) &&
        observation.Fence.RunGeneration == fence.Authority.RunGeneration && observation.Fence.Scope == fence.Authority.Scope &&
        observation.Fence.ServerSessionInstance == fence.Authority.ServerSessionInstance && observation.Fence.ConnectionGeneration is > 0 &&
        observation.BaselineSequence is > 0 && observation.StateVersion >= fence.MinimumStateVersion &&
        observation.Items.SequenceEqual(fence.ExpectedItems) && observation.Hands.Select(h => h with { Evidence = HandEvidence.DomainHandIndex }).SequenceEqual(fence.ExpectedHands) &&
        observation.Hands.All(h => h.Evidence == HandEvidence.ClientProjection);

    public async ValueTask<CleanupResult> CloseAsync(int timeoutMs, CancellationToken cleanupToken)
    {
        CheckOwner(); if (cleanup is not null) return cleanup;
        closing = true;
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cleanupToken);
        bounded.CancelAfter(timeoutMs);
        var errors = new List<string>();
        try
        {
            // Every owned role gets an attempt within the same total budget, even if another OS operation fails.
            foreach (var role in roles.OrderBy(r => r.Id == "server" ? 1 : 0))
            {
                try
                {
                    if (!hasExited(role.Process))
                    {
                        var id = await Send(role, RoleControlKind.Stop, bounded.Token);
                        role.Process.StandardInput.Close();
                        await Receive(role, r => r.Kind == RoleReplyKind.Stopped && r.ControlId == id, bounded.Token, cleanupRead: true);
                    }
                    await role.Process.WaitForExitAsync(bounded.Token);
                    role.ExitCode = readExitCode(role.Process);
                    await role.Reader.WaitAsync(bounded.Token);
                    await role.Diagnostics.WaitAsync(bounded.Token);
                    if (!role.Stopped || role.ExitCode != 0 || role.Error is not null)
                        Record(role, "NoNormalStop/native=" + role.ExitCode + "/" + role.Error);
                }
                catch (Exception e)
                {
                    Record(role, "StopOrExit", e);
                    // Query and termination errors are independent. A failed query still permits an owned-child attempt.
                    bool mayBeAlive = true;
                    try { mayBeAlive = !hasExited(role.Process); }
                    catch (Exception query) { Record(role, "ExitQuery", query); }
                    if (mayBeAlive)
                        try { terminate(role.Process); }
                        catch (Exception kill) { Record(role, "OwnedTermination", kill); }
                }
                finally
                {
                    try
                    {
                        await role.Process.WaitForExitAsync(bounded.Token);
                        role.ExitCode ??= readExitCode(role.Process);
                    }
                    catch (Exception e) { Record(role, "UnconfirmedExit", e); }
                }
            }
        }
        finally
        {
            try { readers.Cancel(); }
            catch (Exception e) { errors.Add("ReaderCancellation:" + e.GetType().Name + ":" + e.Message); }
            foreach (var role in roles)
            {
                try { await role.Reader.WaitAsync(bounded.Token); }
                catch (Exception e) { Record(role, "UnconfirmedReader", e); }
                try { await role.Diagnostics.WaitAsync(bounded.Token); }
                catch (Exception e) { Record(role, "UnconfirmedDiagnostics", e); }
                // Preserve a first role fault separately from later cleanup errors.
                if (role.Error is not null && !errors.Contains(role.Id + ":" + role.Error)) errors.Add(role.Id + ":" + role.Error);
                try { role.Process.Dispose(); }
                catch (Exception e) { Record(role, "HandleDispose", e); }
            }
            try { readers.Dispose(); }
            catch (Exception e) { errors.Add("ReaderTokenDispose:" + e.GetType().Name + ":" + e.Message); }
            try
            {
                var assembly = typeof(Program).Assembly.Location;
                await FlowReport.WriteAtomicAsync(Path.Combine(request.OutputRoot, "network-resources.json"), JsonSerializer.Serialize(new
                {
                    run, parentProcessId = Environment.ProcessId, assembly, assemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly))),
                    children = roles.Select(r => new { roleId = r.Id, processId = r.ProcessId, executable = "dotnet", arguments = new[] { assembly, "role", "--id", r.Id },
                        r.Endpoint, r.Binding, r.ExitCode, r.Stopped, readerComplete = r.Reader.IsCompleted, diagnosticsComplete = r.Diagnostics.IsCompleted,
                        r.Error, r.CleanupErrors, r.Stderr, r.Identity, controls = r.Sent }).ToArray()
                }, FlowJson.Options), 1048576, bounded.Token);
            }
            catch (Exception e) { errors.Add("ResourceEvidenceWrite:" + e.GetType().Name + ":" + e.Message); }
        }
        var resources = roles.Select(r => new ResourceOutcome(r.Id, r.ProcessId, r.ExitCode is not null,
            r.ExitCode == 0 && r.Stopped && r.Error is null && r.CleanupErrors.Count == 0 ? null :
                string.Join(";", new[] { r.Error ?? "NoNormalStop/native=" + r.ExitCode }.Concat(r.CleanupErrors)))).ToArray();
        cleanup = new(errors.Count == 0 ? CleanupState.Complete : CleanupState.Incomplete, resources, errors);
        return cleanup;

        void Record(Role role, string phase, Exception? e = null)
        {
            var detail = phase + (e is null ? "" : ":" + e.GetType().Name + ":" + e.Message);
            role.CleanupErrors.Add(detail); errors.Add(role.Id + ":" + detail);
        }
    }

    private Role Launch(string roleId)
    {
        var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { typeof(Program).Assembly.Location, "role", "--id", roleId }) info.ArgumentList.Add(arg);
        var role = new Role(roleId, Process.Start(info) ?? throw new IOException("ChildStartFailed"));
        roles.Add(role);
        role.Reader = ReadReplies(role); role.Diagnostics = ReadDiagnostics(role);
        return role;
    }
    private async Task ReadReplies(Role role)
    {
        try
        {
            long sequence = 0, eventSequence = 0;
            while (await FlowRoleProtocol.ReadLineAsync(role.Process.StandardOutput.BaseStream, readers.Token) is { } line)
            {
                var reply = FlowJson.Read<RoleReply>(line);
                FlowRoleProtocol.ValidateReply(reply, run, role.Id, ++sequence, reply.Event is null ? eventSequence : ++eventSequence);
                if (!role.Replies.Writer.TryWrite(reply)) throw new InvalidDataException("RoleReplyQueueOverflow");
            }
            role.Replies.Writer.TryComplete();
        }
        catch (Exception e) { role.Error ??= e.Message; role.Replies.Writer.TryComplete(e); }
    }
    private async Task ReadDiagnostics(Role role)
    {
        try
        {
            var bytes = new byte[4096]; var text = new MemoryStream();
            int read;
            while ((read = await role.Process.StandardError.BaseStream.ReadAsync(bytes, readers.Token)) != 0)
            {
                if (text.Length + read > request.Logs.DiagnosticBytesPerHost) throw new InvalidDataException("RoleStderrOverflow");
                text.Write(bytes, 0, read);
            }
            role.Stderr = Encoding.UTF8.GetString(text.ToArray());
        }
        catch (Exception e) { role.Error ??= e.Message; }
    }
    private async Task<string> Send(Role role, RoleControlKind kind, CancellationToken token, string? id = null,
        RoleInit? init = null, FlowAction? action = null, string? replay = null, string? barrier = null, string? point = null)
    {
        CheckOwner(); token.ThrowIfCancellationRequested();
        id ??= "control-" + ++controls;
        var value = new RoleControl(1, run, role.Id, ++role.ControlSequence, id, kind, init, action, replay, barrier, point);
        await FlowRoleProtocol.WriteAsync(role.Process.StandardInput.BaseStream, value, token);
        role.Sent.Add(value);
        CheckOwner(); token.ThrowIfCancellationRequested();
        return id;
    }
    private async Task<RoleReply> Receive(Role role, Func<RoleReply, bool> matches, CancellationToken token, bool cleanupRead = false)
    {
        while (true)
        {
            if (!cleanupRead) CheckActive(token, requireEvidence: false);
            var reply = await role.Replies.Reader.ReadAsync(token);
            CheckOwner();
            // Save real facts before observing caller cancellation or a later role failure.
            Accept(role, reply, throwFault: !cleanupRead);
            if (matches(reply)) return reply;
        }
    }
    private void Accept(Role role, RoleReply reply, bool throwFault)
    {
        if (reply.ControlId is not null && !role.Sent.Any(c => c.ControlId == reply.ControlId))
            throw new InvalidDataException("UnknownRoleReplyControl");
        role.ReplySequence = reply.HostSequence;
        if (reply.Event is { } e)
        {
            role.EventSequence = e.HostSequence;
            if (e.Kind == FlowEventKind.RoleReady)
            {
                using var identity = JsonDocument.Parse(e.Detail ?? throw new InvalidDataException("MissingChildIdentity"));
                if (identity.RootElement.GetProperty("processId").GetInt32() != role.Process.Id ||
                    identity.RootElement.GetProperty("assemblySha256").GetString() != Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Program).Assembly.Location))))
                    throw new InvalidDataException("ChildBinaryIdentityMismatch");
                role.Identity = e.Detail;
            }
            if (e.Observation is { } cut)
            {
                if (role.Binding is not null && cut.Fence != role.Binding) throw new InvalidDataException("RoleObservationBindingChanged");
                if (cut.Origin == ObservationOrigin.Client)
                {
                    try { ValidateClient(cut, roles.Single(r => r.Id == "server").Binding!, role.Id, role.Binding); }
                    catch (InvalidDataException invalid) { role.Error ??= invalid.Message; error ??= invalid.Message; throw; }
                }
                role.Last = cut;
            }
            if (e.Command is { } command)
            {
                if (!calls.TryGetValue(command.CallId, out var original) || original.Role != role || command.FrozenCommand != original.Command ||
                    original.Binding != role.Binding || command.BusinessId != original.Command.Command.Value || command.ActorId != original.Command.Player.Value)
                    throw new InvalidDataException("RoleCommandBindingOrPayload");
                if (!terminals.TryAdd(command.CallId, command)) throw new InvalidDataException("RoleTerminalRepeated");
            }
            if (!events.TryPublish(e)) error ??= "RoleEvidenceIncomplete";
        }
        if (reply.ControlId is not null) role.Controls[reply.ControlId] = reply;
        if (reply.Kind == RoleReplyKind.Stopped) role.Stopped = true;
        if (reply.Kind == RoleReplyKind.Fault)
        {
            role.Error ??= reply.ErrorCode; error ??= reply.ErrorCode;
            if (throwFault) throw new IOException(reply.ErrorCode);
        }
    }
    private static void ValidateClient(FlowObservation cut, BindingFence authority, string id, BindingFence? original)
    {
        if (!cut.Available || cut.SynchronizedObserved != true || cut.Origin != ObservationOrigin.Client || cut.ObserverId != id || cut.BaselineSequence is not > 0 ||
            cut.Fence.RunGeneration != authority.RunGeneration || cut.Fence.Scope != authority.Scope ||
            cut.Fence.ServerSessionInstance != authority.ServerSessionInstance || cut.Fence.ConnectionGeneration is not > 0 ||
            original is not null && cut.Fence != original) throw new InvalidDataException("ClientBindingMismatch");
    }
    private Role Client(string actor) => roles.Single(r => r.Id == (actor == "A" ? "client-a" : "client-b"));
    private void CheckOwner()
    { if (Environment.CurrentManagedThreadId != owner) throw new InvalidOperationException("NetworkAdapterOwnerChanged"); }
    private void CheckActive(CancellationToken token, bool requireEvidence = true)
    {
        CheckOwner(); token.ThrowIfCancellationRequested();
        if (!started || closing || run is null || error is not null || roles.Any(r => r.Error is not null || r.Process.HasExited))
            throw new IOException(error ?? roles.FirstOrDefault(r => r.Error is not null)?.Error ?? "NetworkRoleNotLive");
        if (requireEvidence && !events.EvidenceComplete) throw new IOException("NetworkEvidenceIncomplete");
    }
}
