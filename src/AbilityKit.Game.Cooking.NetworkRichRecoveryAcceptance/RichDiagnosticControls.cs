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
}
