using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Host;
using AbilityKit.Network.Protocol;
using System.Text.Json;
using System.Security.Cryptography;
using AbilityKit.Game.Cooking.RichEvidence;
using AbilityKit.Game.Cooking.Session;
namespace AbilityKit.Game.Cooking.NetworkRichRecoveryAcceptance;

// Pure application metadata controls; no socket, authority creation or business simulation.
internal static class RichDiagnosticControls
{
    public static int Run(string[] args)
    {
        var count = 0;
        void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); count++; Console.WriteLine("CONTROL " + name); }
        void Invalid(string[] args, string name) {
            try { RichCommandPathDiagnostics.Parse(args); } catch (ArgumentException) { Check(true, name); return; }
            throw new InvalidOperationException("Expected rejection: " + name);
        }
        try {
            if (args.Length != 0 && (args.Length != 2 || args[0] != "--legacy-report")) throw new ArgumentException("Only optional --legacy-report <original path> is supported.");
            Check(!RichCommandPathDiagnostics.Parse([]).Enabled, "default OFF");
            Check(RichCommandPathDiagnostics.Create(RichCommandPathDiagnostics.Parse([])) is null, "OFF constructs no diagnostic collector");
            Invalid(["--diagnostics", "ON"], "missing selector");
            Invalid(["--diagnostics"], "missing value");
            Invalid(["--diagnostics", "ON", "--diagnostics", "OFF"], "duplicate flag");
            Invalid(["--diagnostics", "MAYBE"], "unsupported mode");
            Invalid(["--diagnostic-other", "x"], "unsupported option");
            Invalid(["--diagnostic-stable", "x"], "OFF selector rejected");
            var options = RichCommandPathDiagnostics.Parse(["--diagnostics", "ON", "--diagnostic-participant", "natural-partner", "--diagnostic-stable", "process-action-366"]);
            var diagnostics = new RichCommandPathDiagnostics(options);
            var id = diagnostics.BeginWait("terminal-366", "natural-partner", 100, 200, "remote-366", null, 1, 2, 3, null, null, null);
            var nested = diagnostics.BeginWait("nested-cut", "natural-partner", 90, 200, "remote-366", null, 1, 2, 3, null, null, null);
            diagnostics.EndWait(nested, "SUCCESS", true, null, null);
            Check(diagnostics.Export().CurrentWait?.Ordinal == id, "nested parent restored without deadline reset");
            diagnostics.EndWait(id, "OPERATION_EXPIRED", false, null, null);
            var terminal = diagnostics.Export().Waits.Rows.Last();
            Check(terminal.ResultWaitStart == 3 && terminal.ProjectionWaitStart is null && terminal.OperationDeadline == 100 && terminal.WholeDeadline == 200,
                "terminal wait absent projection and exact deadlines");
            var projection = diagnostics.BeginWait("committed-caller-projection", "natural-partner", 110, 200, "remote-366", null, 1, 2, 3, 4, 5555, null);
            diagnostics.EndWait(projection, "SUCCESS", true, 5555, null);
            Check(diagnostics.Export().Waits.Rows.Last().ProjectionWaitStart == 4 && diagnostics.Export().Waits.Rows.Last().TargetResultVersion == 5555,
                "projection start and target distinct");
            for (var i = 0; i < 70; i++) { var wait = diagnostics.BeginWait("ring", "p", 100, 200, null, null, null, null, null, null, null, null); diagnostics.EndWait(wait, "SUCCESS", true, null, null); }
            var report = diagnostics.Export();
            Check(report.Waits.Retained == 64 && report.Waits.TotalSeen == 73 && report.Waits.Overwritten == 9 && report.Waits.FirstOrdinal < report.Waits.LastOrdinal,
                "wait ring exact truncation disclosure");
            Check(report.Callbacks.TotalSeen == 0 && report.Frames.TotalSeen == 0 && report.Snapshots.TotalSeen == 0, "no selected detail before activation");
            Check(report.Waiting == "UNOBSERVABLE_PRIVATE" && report.DeferredAck == "UNOBSERVABLE_PRIVATE", "private absence not numeric zero");
            var scope = new CookingLevelScope(new(new("diagnostic-session"), new("world"), new("match")), new(1), new("service"), 1);
            var wire = new CookingNetworkWireCommand("diagnostic-instance", 1, 1, "process-action-366", scope,
                new(scope.MatchScope, 0, new("natural-partner"), new("process-action-366"), CookingRecipeOperation.Move));
            diagnostics.ObserveCommand(wire, "remote-366");
            Check(diagnostics.Active && diagnostics.SelectedDomain == CookingNetworkWireCodec.DomainId("diagnostic-instance", scope, new("natural-partner"), "process-action-366").Value,
                "selected typed identity maps independently without authority execution");
            RealPaths(Check, options, wire);
            ActualRunFailurePaths(Check);
            var callbackRow = new RichDiagnosticCallback(0, 1, "control", "in", null, 1, "remote-366", "CommandResult", 12,
                10, null, null, null, null, 11, 12, 13, 14, 15, 0, "Complete callback; native UNKNOWN.", null);
            for (var i = 0; i < 300; i++) diagnostics.Callback(callbackRow);
            Check(diagnostics.Export().Callbacks.Retained == 256 && diagnostics.Export().Callbacks.Overwritten == 44,
                "callback ring bounded with exact overwritten count");
            Check(Enumerable.Range(0, 8).All(i => diagnostics.ReserveSnapshot("milestone-" + i)) && !diagnostics.ReserveSnapshot("ninth"),
                "diagnostics getter reservations bounded at8");
            diagnostics.Projection("remote-366"); var prior = diagnostics.Export().Callbacks.TotalSeen;
            diagnostics.Callback(callbackRow);
            Check(diagnostics.Export().Callbacks.TotalSeen == prior, "selected detail closes after projection");
            var segments = new RichCommandPathDiagnostics.CallbackSegments();
            segments.Append(10, 3); segments.Append(11, 7);
            Check(segments.Consume(5).Contains("10..11"), "fragmented framework callbacks mapping");
            Check(segments.Consume(5).Contains("11..11"), "two frames share callback");
            for (var i = 0; i < 257; i++) segments.Append(i, 1);
            Check(segments.Consume(1).Contains("truncated"), "callback segment bound disclosed");
            var text = RichCommandPathDiagnostics.Text(new string('x', 5000));
            Check(text.Text.Length == 4096 && text.OriginalLength == 5000 && text.Truncated, "diagnostic text bound");
            var roundtrip = JsonSerializer.Deserialize<RichCommandPathReport>(JsonSerializer.Serialize(report));
            Check(roundtrip?.CurrentWait?.Status == "SUCCESS" && roundtrip.Waiting == "UNOBSERVABLE_PRIVATE", "metadata serialization retains null and status");
            if (args.Length == 2) {
                var path = Path.GetFullPath(args[1]);
                if (new FileInfo(path).Length > 128L * 1024 * 1024) throw new InvalidOperationException("Legacy report128MiB bound.");
                var original = File.ReadAllBytes(path); var before = SHA256.HashData(original);
                var oldReport = JsonSerializer.Deserialize<RichEndpointReport>(original, CookingNetworkWireCodec.JsonOptions);
                Check(oldReport is not null && oldReport.SchemaVersion == 1 && oldReport.DiagnosticOptions is null && oldReport.CommandPathDiagnostics is null,
                    "actual legacy optional metadata compatibility");
                Check(SHA256.HashData(File.ReadAllBytes(path)).AsSpan().SequenceEqual(before), "original legacy bytes unchanged");
            }
            Console.WriteLine("DIAGNOSTIC_CONTROLS " + count + " PASS; application metadata only, not ET/transport evidence.");
            return 0;
        } catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    private static void ActualRunFailurePaths(Action<bool, string> check)
    {
        var directory = Path.Combine(Path.GetTempPath(), "rich-error-control-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var priorError = Console.Error;
        try {
            foreach (var fallbackFails in new[] { false, true }) {
                var destination = Path.Combine(directory, fallbackFails ? "existing-directory" : "failure.json");
                if (fallbackFails) Directory.CreateDirectory(destination);
                using var captured = new StringWriter(); Console.SetError(captured);
                // Invalid role is rejected before Host/Client and before authority/socket creation.
                var code = new RichRunner("invalid-role", "manual-paused", "error-control", "127.0.0.1", 0,
                    "ApplicationNoSocketControl", new string('a', 40), "clean", destination).Run().GetAwaiter().GetResult();
                Console.SetError(priorError);
                var stderr = captured.ToString();
                check(code == 1 && stderr.Contains("ORIGINAL_FAILURE") && stderr.Contains("Role.") && stderr.Contains("EXPORT_FAILURE"),
                    "actual Run original/export failure retained " + fallbackFails);
                if (fallbackFails) check(stderr.Contains("FALLBACK_EXPORT_FAILURE") && Directory.Exists(destination),
                    "actual Run unwritable fallback preserves all errors and returns nonzero");
                else {
                    using var report = JsonDocument.Parse(File.ReadAllBytes(destination));
                    check(!report.RootElement.GetProperty("passed").GetBoolean() &&
                        report.RootElement.GetProperty("failure").GetString()!.Contains("Role.") &&
                        report.RootElement.TryGetProperty("exportFailure", out _), "actual fallback report preserves original bounded error");
                }
            }
        } finally {
            Console.SetError(priorError);
            // Only this freshly owned, fixed-prefix temporary directory is removed.
            var resolved = Path.GetFullPath(directory);
            var tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!resolved.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(resolved).StartsWith("rich-error-control-", StringComparison.Ordinal))
                throw new InvalidOperationException("Owned temporary control directory boundary.");
            if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
        }
    }
    private static void RealPaths(Action<bool, string> check, RichDiagnosticOptions options, CookingNetworkWireCommand selected)
    {
        byte[] Frame(uint opcode, byte[] payload) {
            var frame = new RichFrameCodec().Encode(new NetworkPacketHeader(NetworkPacketFlags.ServerPush, opcode, 1, (uint)payload.Length), new(payload));
            return frame.ToArray();
        }
        var command = Frame(CookingNetworkWireCodec.OpCode, CookingNetworkWireCodec.Encode(CookingNetworkMessageKind.Command, "remote-366", selected));
        foreach (var enabled in new[] { false, true }) {
            var d = RichCommandPathDiagnostics.Create(enabled ? options : new(false, null, null));
            d?.ObserveCommand(selected, "remote-366");
            var listener = new ProbeListener(); using var observer = new RichObserver(listener, d);
            var channel = new ProbeChannel(); IServerChannel? observedChannel = null; var forwarded = new List<byte>(); var calls = 0;
            observer.ChannelAccepted += accepted => { observedChannel = accepted; accepted.BytesReceived += bytes => { calls++; forwarded.AddRange(bytes); }; };
            observer.Start(); listener.Accept(channel);
            channel.Emit(command[..3]); channel.Emit(command[3..]);
            channel.Emit(command.Concat(command).ToArray());
            check(calls == 3 && forwarded.SequenceEqual(command.Concat(command).Concat(command)), "actual observer copy/forward once fragmented and multiple frames " + enabled);
            check(observer.Received.Count == 3, "actual observer original frame observations " + enabled);
            channel.Emit(Frame(17, [1, 2])); channel.Emit(Frame(CookingNetworkWireCodec.OpCode, [1, 2]));
            observedChannel!.Send(new(Frame(17, [1, 2]))); observedChannel.Send(new(Frame(CookingNetworkWireCodec.OpCode, [1, 2])));
            check(channel.SendCalls == 2, "actual outbound forwarding once despite observation rejection " + enabled);
            check(observer.Failure is null && observer.Received.Count == 3, "raw rejection preserves original skip disposition " + enabled);
            if (d is not null) {
                var rows = d.Export().Callbacks.Rows;
                check(rows.Count(x => x.Source == "observer.raw-envelope-rejected" && x.Failure is not null) == 4, "real inbound/outbound opcode/codec failures retained bounded");
                check(rows.Any(x => x.FrameMapping.Contains("1..2")), "actual fragmented callback mapping");
                check(rows.Count(x => x.Source == "observer.typed-read-validation") == 3, "typed reads separately bracketed");
            } else check(d is null, "OFF actual observer has no collector");
        }
        var faultDiagnostics = new RichCommandPathDiagnostics(options); faultDiagnostics.ObserveCommand(selected, "remote-366");
        var faultListener = new ProbeListener(); using var faultObserver = new RichObserver(faultListener, faultDiagnostics);
        var faultChannel = new ProbeChannel(); var original = new InvalidOperationException("original-forward-control");
        faultObserver.ChannelAccepted += accepted => accepted.BytesReceived += _ => throw original;
        faultObserver.Start(); faultListener.Accept(faultChannel);
        try { faultChannel.Emit(command); throw new InvalidOperationException("Missing forward error."); }
        catch (InvalidOperationException error) when (ReferenceEquals(error, original)) { }
        check(faultDiagnostics.Export().Callbacks.Rows.Single().Failure?.Text.Contains("original-forward-control") == true,
            "actual forward original exception retained and rethrown");

        foreach (var peerEnabled in new[] { false, true }) {
        var peerDiagnostics = RichCommandPathDiagnostics.Create(peerEnabled ? options : new(false, null, null)); peerDiagnostics?.ObserveCommand(selected, "remote-366");
        var transports = new List<ProbeTransport>();
        using var peer = new RichPeer(new("natural-partner"), "probe", () => { var t = new ProbeTransport(); transports.Add(t); return t; }, peerDiagnostics);
        peer.Open("probe", 1);
        var joined = Frame(CookingNetworkWireCodec.OpCode, CookingNetworkWireCodec.Encode(CookingNetworkMessageKind.Joined, "probe-joined",
            new CookingNetworkJoined("diagnostic-instance", new("natural-partner"), 1, "probe-token")));
        transports[0].Emit(joined); // Real ConnectionManager assembly and actual callback enqueue.
        check(peer.DiagnosticState().QueueCount == 1 && peer.DiagnosticState().Generation is null, "actual peer callback queues before owner Poll");
        peer.Reopen("probe", 1); peer.Poll();
        check(peer.DiagnosticState().QueueCount == 0 && peer.DiagnosticState().Generation is null, "actual old-incarnation queued envelope ignored");
        transports[1].Emit(joined); peer.Poll();
        check(peer.DiagnosticState().Generation == 1 && (peerDiagnostics is null || peerDiagnostics.Export().Callbacks.Rows.Any(x => x.Source == "peer.typed-receive-validation" && x.Installed is not null && x.DecodeStart is not null && x.DecodeEnd >= x.DecodeStart)), "actual current-incarnation typed Joined installed");
        transports[1].Emit(Frame(CookingNetworkWireCodec.OpCode, [1, 2]));
        check(peer.Failure is not null, "actual malformed callback records failure");
        try { peer.Poll(); throw new InvalidOperationException("Missing passive callback failure."); }
        catch (InvalidOperationException error) when (error.InnerException == peer.Failure) { }
        check(peerDiagnostics is null || peerDiagnostics.Export().Callbacks.Rows.Any(x => x.Failure is not null), "actual callback error preserves ON/OFF semantics " + peerEnabled);
        }
    }
    private sealed class ProbeListener : IChannelListener
    {
        public bool IsListening { get; private set; } public string Endpoint => "probe";
        public event Action<IServerChannel>? ChannelAccepted; public event Action<Exception>? Error;
        public void Start() => IsListening = true; public void Stop() => IsListening = false;
        public void Accept(IServerChannel channel) => ChannelAccepted?.Invoke(channel);
        public void Dispose() => Stop();
    }
    private sealed class ProbeChannel : IServerChannel
    {
        public string Id => "probe-channel"; public string RemoteEndpoint => "probe"; public bool IsConnected => true;
        public event Action<ArraySegment<byte>>? BytesReceived; public event Action<IServerChannel>? Closed; public event Action<Exception>? Error;
        public int SendCalls { get; private set; }
        public void Emit(byte[] bytes) => BytesReceived?.Invoke(new(bytes)); public void Send(ArraySegment<byte> bytes) { SendCalls++; }
        public void Close() => Closed?.Invoke(this); public void Dispose() { }
    }
    private sealed class ProbeTransport : ITransport
    {
        public bool IsConnected { get; private set; }
        public event Action? Connected; public event Action? Disconnected; public event Action<Exception>? Error; public event Action<ArraySegment<byte>>? BytesReceived;
        public void Connect(string host, int port) { IsConnected = true; Connected?.Invoke(); }
        public void Emit(byte[] bytes) => BytesReceived?.Invoke(new(bytes)); public void Send(ArraySegment<byte> bytes) { }
        public void Close() { IsConnected = false; Disconnected?.Invoke(); } public void Dispose() => Close();
    }

}
