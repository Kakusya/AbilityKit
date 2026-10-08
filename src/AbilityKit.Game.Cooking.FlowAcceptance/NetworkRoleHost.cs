using System.Diagnostics;
using System.Net;
using System.Threading.Channels;
using System.Security.Cryptography;
using System.Text.Json;
using AbilityKit.Game.Cooking.EtRuntime;
using AbilityKit.Game.Cooking.Session;
using AbilityKit.Network.Transport.LiteNet;

namespace AbilityKit.Game.Cooking.FlowAcceptance;

internal static class NetworkRoleHost
{
    private sealed record Invocation(CookingRecipeCommand Command, BindingFence Binding, string Step);
    private sealed record Pending(string Call, Invocation Invocation, Task<CookingNetworkWireResult> Response,
        CancellationTokenSource Wait);

    internal static async Task<int> RunAsync(string roleId, Stream controlInput, Stream replyOutput,
        TextWriter diagnostics, CancellationToken cancellationToken)
    {
        var owner = Environment.CurrentManagedThreadId;
        var clock = Stopwatch.StartNew();
        using var readerCancellation = new CancellationTokenSource();
        var controls = Channel.CreateBounded<RoleControl>(new BoundedChannelOptions(256)
        { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait });
        var reader = ReadControls();
        RunIdentity? run = null;
        RoleInit? init = null;
        CookingLevelEtHost? authority = null;
        CookingNetworkSessionHost? server = null;
        CookingNetworkSessionClient? client = null;
        Task? joining = null;
        CancellationTokenSource? joinWait = null;
        BindingFence? binding = null;
        string? initializeId = null;
        long replySequence = 0, eventSequence = 0;
        bool ready = false, stopped = false;
        int exit = 2;
        var seenControls = new Dictionary<string, RoleControl>(StringComparer.Ordinal);
        var calls = new Dictionary<string, Invocation>(StringComparer.Ordinal);
        var armed = new Dictionary<string, (string Call, Invocation Invocation)>(StringComparer.Ordinal);
        var released = new HashSet<string>(StringComparer.Ordinal);
        var pending = new List<Pending>();
        try
        {
            while (!stopped)
            {
                CheckOwner(); cancellationToken.ThrowIfCancellationRequested();
                if (clock.ElapsedMilliseconds >= (ready ? init!.Budgets.OverallMs - init.Budgets.ResetMs - init.Budgets.PublishMs : 10000))
                    throw new TimeoutException(ready ? "RoleActiveDeadline" : "RoleStartupDeadline");
                // The only authority frame driver; it stays active while stdin/Join/commands wait.
                if (server is not null)
                {
                    server.ProcessOwnerFrame();
                    if (authority!.IsFaulted) throw new InvalidOperationException("AuthorityFaulted");
                }
                if (joining is { IsCompleted: true } && !ready)
                {
                    await joining;
                    CheckOwner(); cancellationToken.ThrowIfCancellationRequested();
                    var current = CaptureClient();
                    if (!current.Available || current.SynchronizedObserved != true) throw new InvalidOperationException("ClientBaselineUnavailable");
                    binding = current.Fence;
                    ready = true; exit = 1;
                    await Emit(RoleReplyKind.Ready, FlowEventKind.RoleReady, "initial", initializeId, observation: current);
                }
                foreach (var p in pending.Where(p => p.Response.IsCompleted).ToArray())
                {
                    var observation = ObserveTerminal(p);
                    pending.Remove(p); p.Wait.Dispose();
                    await Emit(RoleReplyKind.Event, FlowEventKind.CommandObserved, p.Invocation.Step, p.Call, command: observation);
                    CheckLive();
                }
                if (controls.Reader.TryRead(out var control))
                {
                    if (seenControls.TryGetValue(control.ControlId, out var previous))
                    {
                        if (control with { ControlSequence = previous.ControlSequence } != previous)
                            throw new InvalidDataException("RoleControlIdentityConflict");
                        // Duplicate Arm/Release does not send the command again or create a second terminal.
                        if (control.Kind == RoleControlKind.ArmAction)
                            await Reply(RoleReplyKind.Armed, control.ControlId);
                        continue;
                    }
                    if (seenControls.Count >= 256) throw new InvalidDataException("RoleControlLimit");
                    seenControls.Add(control.ControlId, control);
                    if (control.Kind == RoleControlKind.Initialize)
                    {
                        run = control.Run; init = control.Init!; initializeId = control.ControlId;
                        if (roleId == "server")
                        {
                            authority = new FlowFixture(run).CreateHost();
                            server = new(new CookingNetworkAuthorityAdapter(authority),
                                new LiteNetChannelListener(IPAddress.Loopback, 0, "abilitykit-cooking-v3"),
                                new Dictionary<PlayerId, string> { [new("A")] = "flow-A", [new("B")] = "flow-B" });
                            server.Start();
                            binding = new(run.RunGeneration, authority.Binding.LevelScope, server.ServerSessionInstance, null);
                            ready = true; exit = 1;
                            await Emit(RoleReplyKind.Ready, FlowEventKind.RoleReady, "initial", control.ControlId,
                                observation: CaptureAuthority(), endpoint: server.Endpoint);
                        }
                        else
                        {
                            client = new(new(init.ActorId!), "flow-" + init.ActorId);
                            FlowRoleProtocol.TryEndpoint(init.Endpoint, out var endpoint);
                            joinWait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                            joinWait.CancelAfter(Math.Max(1, 10000 - (int)clock.ElapsedMilliseconds));
                            joining = client.ConnectAsync(endpoint.Address.ToString(), endpoint.Port, joinWait.Token);
                        }
                    }
                    else if (control.Kind == RoleControlKind.Stop)
                    {
                        // Stop is independent of gameplay/caller cancellation. No new frames/actions after this point.
                        stopped = true;
                        await Cleanup();
                        await Emit(RoleReplyKind.Stopped, FlowEventKind.RoleStopped, "cleanup", control.ControlId);
                        exit = 0;
                    }
                    else
                    {
                        CheckLive();
                        switch (control.Kind)
                        {
                            case RoleControlKind.ArmAction:
                                if (client is null || released.Contains(control.BarrierId!) || armed.ContainsKey(control.BarrierId!))
                                    throw new InvalidDataException("RoleArmPrecondition");
                                Invocation invocation;
                                if (control.Action is { } action)
                                {
                                    ValidateAction(action);
                                    invocation = new(new(binding!.Scope.MatchScope, 0, new(action.ActorId), new(action.BusinessId),
                                        action.Verb == FlowVerb.Pickup ? CookingRecipeOperation.Pickup : CookingRecipeOperation.Drop,
                                        Item: new ItemId(action.ItemId), Station: action.StationSlot is null ? null : new StationSlotId(action.StationSlot),
                                        ExpectedItemVersion: action.ExpectedItemVersion), binding!, control.BarrierId!);
                                }
                                else
                                {
                                    if (!calls.TryGetValue(control.ReplayOfCallId!, out var original) || original.Binding != binding)
                                        throw new InvalidDataException("RoleReplayBindingOrCall");
                                    invocation = original with { Step = control.BarrierId! };
                                }
                                if (!calls.TryAdd(control.ControlId, invocation)) throw new InvalidDataException("RoleCallReused");
                                armed.Add(control.BarrierId!, (control.ControlId, invocation));
                                await Reply(RoleReplyKind.Armed, control.ControlId);
                                break;
                            case RoleControlKind.ReleaseBarrier:
                                if (client is null || !armed.Remove(control.BarrierId!, out var actionToSend) ||
                                    !released.Add(control.BarrierId!)) throw new InvalidDataException("RoleReleasePrecondition");
                                var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                                wait.CancelAfter(Math.Max(1, Math.Min(init!.Budgets.StepMs,
                                    init.Budgets.OverallMs - init.Budgets.ResetMs - init.Budgets.PublishMs - (int)clock.ElapsedMilliseconds)));
                                // Send the exact frozen wire payload, including batch0/version, with a fresh native correlation.
                                var response = client.SendCommandAsync(actionToSend.Invocation.Command.Command.Value, actionToSend.Invocation.Command, wait.Token);
                                pending.Add(new(actionToSend.Call, actionToSend.Invocation, response, wait));
                                await Emit(RoleReplyKind.Event, FlowEventKind.StepStarted, control.BarrierId!, control.ControlId);
                                break;
                            case RoleControlKind.Observe:
                                var cut = server is not null ? CaptureAuthority() : CaptureClient();
                                await Emit(RoleReplyKind.Event, FlowEventKind.StateObserved, control.ObservationPoint!, control.ControlId, observation: cut);
                                break;
                            default: throw new InvalidDataException("RoleControlPrecondition");
                        }
                    }
                }
                else if (controls.Reader.Completion.IsCompleted)
                {
                    await controls.Reader.Completion;
                    throw new EndOfStreamException("ControlEOFBeforeStop");
                }
                if (!stopped)
                {
                    await Task.Delay(5, cancellationToken);
                    CheckOwner();
                    if (ready) CheckLive();
                }
            }
        }
        catch (Exception e)
        {
            exit = e is InvalidDataException || !ready ? 2 : 1;
            await diagnostics.WriteLineAsync(e.GetType().Name + ": " + e.Message);
            // Real completed terminal facts survive a later control/frame fault.
            foreach (var p in pending.Where(p => p.Response.IsCompleted).ToArray())
            {
                try { await Emit(RoleReplyKind.Event, FlowEventKind.CommandObserved, p.Invocation.Step, p.Call, command: ObserveTerminal(p)); }
                catch (Exception) { /* Broken reply pipe cannot be presented as delivered evidence. */ }
                pending.Remove(p); p.Wait.Dispose();
            }
            if (run is not null)
                try { await Emit(RoleReplyKind.Fault, FlowEventKind.RoleFaulted, "role", null, error: e.GetType().Name + ":" + e.Message); }
                catch (Exception) { }
        }
        finally
        {
            try { await Cleanup(); }
            catch (Exception e) { exit = 1; await diagnostics.WriteLineAsync("Cleanup:" + e.GetType().Name); }
            joinWait?.Dispose();
        }
        return exit;

        async Task ReadControls()
        {
            RunIdentity? frozen = null;
            long sequence = 0;
            try
            {
                while (await FlowRoleProtocol.ReadLineAsync(controlInput, readerCancellation.Token) is { } line)
                {
                    var control = FlowJson.Read<RoleControl>(line);
                    FlowRoleProtocol.ValidateControl(control, roleId, frozen, ++sequence);
                    frozen ??= control.Run;
                    if (!controls.Writer.TryWrite(control)) throw new InvalidDataException("RoleControlQueueOverflow");
                }
                controls.Writer.TryComplete();
            }
            catch (Exception e) { controls.Writer.TryComplete(e); }
        }
        void CheckOwner()
        {
            if (Environment.CurrentManagedThreadId != owner) throw new InvalidOperationException("RoleOwnerThreadChanged");
        }
        void CheckLive()
        {
            CheckOwner(); cancellationToken.ThrowIfCancellationRequested();
            if (!ready || stopped || run is null || binding is null || binding.RunGeneration != run.RunGeneration)
                throw new InvalidOperationException("RoleNotReadyOrRetired");
            if (server is not null && (authority!.Binding.LevelScope != binding.Scope || server.ServerSessionInstance != binding.ServerSessionInstance))
                throw new InvalidOperationException("RoleAuthorityBindingChanged");
            if (client is not null)
            {
                var current = CaptureClient();
                if (!current.Available || current.Fence != binding) throw new InvalidOperationException("RoleClientBindingChanged");
            }
        }
        void ValidateAction(FlowAction action)
        {
            FlowJson.RequireId(action.CallId); FlowJson.RequireId(action.BusinessId);
            if (action.ActorId != init!.ActorId || !Enum.IsDefined(action.Verb) || action.ItemId != FlowFixture.Spec.ItemId ||
                action.ExpectedItemVersion <= 0 || (action.Verb == FlowVerb.Pickup ? action.StationSlot is not null : action.StationSlot != FlowFixture.Spec.DropSlot))
                throw new InvalidDataException("RoleActionPayload");
        }
        FlowObservation CaptureAuthority()
        {
            var capture = authority!.CaptureReadOnlyFullState();
            if (!capture.Accepted || capture.State?.Observation.Recipe is null)
                return Unavailable("authority", ObservationOrigin.Authority, capture.Reason.ToString());
            if (!authority.TryPeekBoundKitchen(out var borrowed) || borrowed is null) throw new InvalidOperationException("IdleKitchenUnavailable");
            var hands = FlowFixture.Spec.Actors.Select(a => new HandProbe(a, borrowed.ItemInHand(new(a))?.Value, HandEvidence.DomainHandIndex)).ToArray();
            var events = borrowed.EventHistory.ToArray();
            borrowed = null;
            var state = capture.State.Observation;
            return Project("authority", ObservationOrigin.Authority, binding!, state, null, null, hands, events);
        }
        FlowObservation CaptureClient()
        {
            var baseline = client!.LatestBaseline;
            if (baseline is null || baseline.Identity.ServerSessionInstance != client.ServerSessionInstance)
                return Unavailable(roleId, ObservationOrigin.Client, "NoCurrentInstalledBaseline");
            var current = new BindingFence(run!.RunGeneration, baseline.Identity.Scope, baseline.Identity.ServerSessionInstance,
                baseline.Identity.ConnectionGeneration);
            return Project(roleId, ObservationOrigin.Client, current, baseline.State.Observation, baseline.Identity.SnapshotSequence,
                client.IsSynchronized, baseline.State.Observation.Players.Select(p => new HandProbe(p.Id.Value, p.HeldItem?.Value,
                    HandEvidence.ClientProjection)).ToArray(), Array.Empty<CookingRecipeEvent>());
        }
        FlowObservation Unavailable(string observer, ObservationOrigin origin, string error) => new(observer, origin, false, error,
            binding ?? new(run!.RunGeneration, new FlowFixture(run).Scope, null, null), null, null, null, null, false,
            Array.Empty<ItemProbe>(), Array.Empty<HandProbe>(), Array.Empty<CookingRecipeEvent>());
        CommandObservation ObserveTerminal(Pending p)
        {
            var command = p.Invocation.Command;
            CookingNetworkWireResult? result = null;
            string? error = null;
            try { result = p.Response.GetAwaiter().GetResult(); }
            catch (Exception e) { error = e is OperationCanceledException ? "CommandTimeoutOrCancelledAfterSend" : e.GetType().Name; }
            if (result is not null && result.Result is not null && (result.StableCommandId != command.Command.Value ||
                result.DomainCommandId is null || result.DomainCommandId.Value != CookingNetworkWireCodec.DomainId(
                    p.Invocation.Binding.ServerSessionInstance!, p.Invocation.Binding.Scope, command.Player, command.Command.Value)))
                error ??= "ResponseDomainOrStableIdentityMismatch";
            return new(p.Invocation.Step, p.Call, result?.StableCommandId ?? command.Command.Value, command.Player.Value, command, null, null,
                result?.Disposition?.ToString(), result?.DomainCommandId?.Value, null, result?.Reason,
                result?.Result, error is null && result?.Result is not null && result.Disposition is not null ? CallCompletion.Terminal :
                    result is not null ? CallCompletion.ProtocolRejected : CallCompletion.Unknown, error ?? (result?.Result is null ? result?.Reason : null));
        }
        async Task Reply(RoleReplyKind kind, string? control, FlowEvent? e = null, string? endpoint = null, string? error = null)
        {
            using var publish = new CancellationTokenSource(2000);
            await FlowRoleProtocol.WriteAsync(replyOutput, new RoleReply(1, run!, roleId, ++replySequence, control, kind, e, endpoint, error), publish.Token);
            CheckOwner();
        }
        Task Emit(RoleReplyKind kind, FlowEventKind eventKind, string step, string? control, CommandObservation? command = null,
            FlowObservation? observation = null, string? endpoint = null, string? error = null) =>
            Reply(kind, control, new(1, run!.RunId, roleId, run.RunGeneration, ++eventSequence, step, command?.CallId,
                eventKind, observation?.LogicalTick, command, observation, null, error,
                eventKind == FlowEventKind.RoleReady ? JsonSerializer.Serialize(new
                { processId = Environment.ProcessId, executable = Environment.ProcessPath, assembly = typeof(Program).Assembly.Location,
                    assemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Program).Assembly.Location))),
                    runtime = Environment.Version.ToString(), ownerThread = owner }, FlowJson.Options) : null), endpoint, error);
        async Task Cleanup()
        {
            CheckOwner();
            readerCancellation.Cancel(); joinWait?.Cancel();
            foreach (var p in pending) p.Wait.Cancel();
            // Dispose each owner separately, at an idle boundary; session disposal is not authority disposal.
            client?.Dispose(); client = null;
            server?.Dispose(); server = null;
            authority?.Dispose(); authority = null;
            using var cleanup = new CancellationTokenSource(init?.Budgets.ResetMs ?? 10000);
            await reader.WaitAsync(cleanup.Token);
            if (joining is not null) { try { await joining.WaitAsync(cleanup.Token); } catch (Exception) when (joining.IsCompleted) { } }
            foreach (var p in pending)
            {
                try { await p.Response.WaitAsync(cleanup.Token); } catch (Exception) when (p.Response.IsCompleted) { }
                p.Wait.Dispose();
            }
            pending.Clear(); CheckOwner();
        }
    }

    private static FlowObservation Project(string id, ObservationOrigin origin, BindingFence fence, CookingLevelObservation state,
        long? sequence, bool? synchronized, IReadOnlyList<HandProbe> hands, IReadOnlyList<CookingRecipeEvent> events) =>
        new(id, origin, state.Recipe is not null, state.Recipe is null ? "RecipeUnavailable" : null, fence,
            state.Recipe?.Version, state.HostFrameSequence, state.Recipe?.LogicalTick, sequence, synchronized,
            state.Recipe?.Items.Select(i => new ItemProbe(i.Id.Value, i.Version, false, i.Location.Kind.ToString(), i.Location.OwnerId,
                i.Location.SlotId)).ToArray() ?? Array.Empty<ItemProbe>(), hands, events);
}
