using System.Net;
using System.Text;
using System.Text.Json;
using System.Diagnostics;

namespace AbilityKit.Game.Cooking.FlowAcceptance;

public sealed class FlowReport : IFlowReportWriter
{
    public async Task PublishAsync(string runDirectory, FlowResult result, FailurePack? failure, CancellationToken publishToken)
    {
        var publish = Stopwatch.StartNew();
        if (result.Status == FlowStatus.Passed && (!result.ExecutionComplete || result.Verdict != FlowVerdict.Passed ||
            !result.EvidenceComplete || result.Cleanup.State != CleanupState.Complete || result.Failures.Count != 0))
            throw new InvalidDataException("Incomplete result cannot be published as Passed.");
        if (failure is not null && (failure.Run != result.Run || failure.Source != result.Source ||
            failure.Cleanup.State != result.Cleanup.State || !failure.Cleanup.Resources.SequenceEqual(result.Cleanup.Resources) ||
            !failure.Cleanup.Errors.SequenceEqual(result.Cleanup.Errors)))
            throw new InvalidDataException("Failure pack source/run/cleanup must match the result.");
        if (failure is not null)
            await WriteAtomicAsync(Path.Combine(runDirectory, "failure-pack.json"), JsonSerializer.Serialize(failure, FlowJson.Options), 2097152, publishToken);
        var summary = Summary(result);
        await WriteAtomicAsync(Path.Combine(runDirectory, "summary.txt"), summary, 8192, publishToken);
        var rows = string.Join("", result.Checks.Select(c => "<tr><td>" + E(c.Rule.Id) + "</td><td>" + c.Verdict +
            "</td><td>" + E(c.StepId) + "</td><td>" + E(c.Actual) + "</td></tr>"));
        var html = "<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><title>Cooking S1 fixed flow</title>" +
            "<style>body{font:16px system-ui;max-width:1000px;margin:3rem auto;padding:1rem}table{border-collapse:collapse}td,th{padding:.6rem;border:1px solid #aaa}pre{white-space:pre-wrap}</style>" +
            "<h1>Cooking fixed flow: " + result.Status + "</h1><pre>" + E(summary) + "</pre><table><tr><th>Rule</th><th>Verdict</th><th>Step</th><th>Fact</th></tr>" +
            rows + "</table><p>CONVERGE: N/A in S1. Unity, network, physical LAN and automatic Orca wake: NotRun.</p>" +
            "<p>Collector order is receipt order, not cross-host causality. Cancellation never claims undo. Normal process/file delivery only.</p></html>";
        await WriteAtomicAsync(Path.Combine(runDirectory, "report.html"), html, 131072, publishToken);
        // Result is the completion marker and is always the final atomic file publication.
        result = result with { Timing = result.Timing with { TotalMs = result.Timing.TotalMs + publish.ElapsedMilliseconds } };
        await WriteAtomicAsync(Path.Combine(runDirectory, "result.json"), JsonSerializer.Serialize(result, FlowJson.Options), 524288, publishToken);
    }

    public static string Summary(FlowResult result) =>
        $"{result.Status}: {result.FlowId}/v{result.FlowVersion}; run={result.Run.RunId}; request={result.Run.RequestId}\n" +
        $"execution={result.ExecutionComplete}; product={result.Verdict}; evidence={result.EvidenceComplete}; cleanup={result.Cleanup.State}\n" +
        $"goals={string.Join(",", result.CompletedGoals)}; firstFailure={result.Failures.FirstOrDefault()?.Code ?? "none"}\n" +
        $"source={result.Source.Commit}; dirty={result.Source.Dirty}; configuration={result.Source.Configuration}; tool={result.Source.ToolVersion}\n" +
        $"buildMs={result.Timing.BuildMs?.ToString() ?? "NotRun in CLI"}; startupMs={result.Timing.StartupMs}; executeMs={result.Timing.ExecuteMs}; resetMs={result.Timing.ResetMs}; totalMs={result.Timing.TotalMs}\n" +
        $"unverified={string.Join(",", result.UnverifiedGoals)}\n";

    internal static async Task WriteAtomicAsync(string path, string text, int maxBytes, CancellationToken token)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        if (bytes.Length > maxBytes) throw new InvalidDataException("Bounded report limit exceeded.");
        var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
        {
            await stream.WriteAsync(bytes, token);
            await stream.FlushAsync(token);
        }
        token.ThrowIfCancellationRequested();
        File.Move(temporary, path, overwrite: false);
    }

    private static string E(string value) => WebUtility.HtmlEncode(value);
}
