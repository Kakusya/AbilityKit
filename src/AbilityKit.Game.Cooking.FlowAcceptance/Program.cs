using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using AbilityKit.Game.Cooking.NetworkRichRecoveryAcceptance;

namespace AbilityKit.Game.Cooking.FlowAcceptance;

public static class Program
{
    public static int Main(string[] args) => SingleThreadOwner.Run(async () =>
    {
        using var cancelled = new CancellationTokenSource();
        ConsoleCancelEventHandler onCancel = (_, e) => { e.Cancel = true; cancelled.Cancel(); };
        Console.CancelKeyPress += onCancel;
        try
        {
            var actualInvocation = CaptureInvocation();
            var parsed = ParseArguments(args);
            var request = FlowJson.ReadRequest(parsed.RequestPath);
            if (parsed.OutputRoot is not null) request = request with { OutputRoot = parsed.OutputRoot };
            request = request with { OutputRoot = Path.GetFullPath(request.OutputRoot) };
            FlowJson.Validate(request);
            var catalog = new FlowRuleCatalog();
            catalog.ResolveApproved(request.Rules, request.Mode);
            if (parsed.ExitBeforeRun)
            {
                // No RunIdentity, orchestrator, session, collector or gameplay resource exists here.
                var staging = request.OutputRoot + ".diagnostic-" + Guid.NewGuid().ToString("N");
                Directory.CreateDirectory(staging);
                Directory.Move(staging, request.OutputRoot);
                using var saveToken = CancellationTokenSource.CreateLinkedTokenSource(cancelled.Token);
                saveToken.CancelAfter(request.Budgets.StartupMs);
                await FlowReport.WriteAtomicAsync(Path.Combine(request.OutputRoot, "request.json"),
                    JsonSerializer.Serialize(request, FlowJson.Options), 65536, saveToken.Token);
                Console.Error.WriteLine("DiagnosticExitBeforeRun request=" + request.RequestId + "; output=" + request.OutputRoot +
                    "; invocation=" + JsonSerializer.Serialize(actualInvocation, FlowJson.Options));
                return 86;
            }
            var source = CaptureSource();
            var assembly = Assembly.GetExecutingAssembly().Location;
            var orchestrator = new FlowOrchestrator(new OfflineFlowSessionFactory(), catalog, new FlowReport(), source);
            var options = new FlowRunOptions(actualInvocation, parsed.DiagnosticFault);
            var result = await orchestrator.RunAsync(request, options, cancelled.Token);
            Console.Write(FlowReport.Summary(result));
            Console.WriteLine("assemblySha256=" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly))));
            Console.WriteLine("result=" + Path.Combine(Path.GetFullPath(request.OutputRoot), "result.json"));
            return FlowOrchestrator.ExitCode(result.Status);
        }
        catch (Exception e)
        { Console.Error.WriteLine("Blocked: " + e.GetType().Name + ": " + e.Message); return 2; }
        finally { Console.CancelKeyPress -= onCancel; }
    });

    private sealed record CliArguments(string RequestPath, string? OutputRoot, FlowDiagnosticFault DiagnosticFault, bool ExitBeforeRun);

    private static CliArguments ParseArguments(string[] args)
    {
        if (args.Length < 3 || args[0] != "run") throw new InvalidDataException("Usage: run --request <file> [--output-root <fresh-directory>] [--diagnostic-fault fail-after-start | --diagnostic-exit-before-run]");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        string? request = null, output = null;
        var diagnostic = FlowDiagnosticFault.None;
        bool exitBefore = false;
        for (var index = 1; index < args.Length; index++)
        {
            var option = args[index];
            if (!seen.Add(option)) throw new InvalidDataException("Duplicate CLI option.");
            if (option == "--diagnostic-exit-before-run") { exitBefore = true; continue; }
            if (option is not ("--request" or "--output-root" or "--diagnostic-fault") || index + 1 >= args.Length ||
                string.IsNullOrWhiteSpace(args[index + 1])) throw new InvalidDataException("Unknown or missing CLI argument.");
            var value = args[++index];
            switch (option)
            {
                case "--request": request = value; break;
                case "--output-root": output = value; break;
                case "--diagnostic-fault":
                    if (value != "fail-after-start") throw new InvalidDataException("Unknown diagnostic fault.");
                    diagnostic = FlowDiagnosticFault.FailAfterStart; break;
            }
        }
        if (request is null || exitBefore && diagnostic != FlowDiagnosticFault.None)
            throw new InvalidDataException("Request required; diagnostic options are mutually exclusive.");
        return new(request, output, diagnostic, exitBefore);
    }

    private static FlowInvocation CaptureInvocation()
    {
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Actual process executable unavailable.");
        var args = Environment.GetCommandLineArgs();
        // Hosted dotnet retains the managed assembly as its first argument; an apphost does not.
        var arguments = Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase)
            ? args : args.Skip(1).ToArray();
        return new(executable, Array.AsReadOnly(arguments), Directory.GetCurrentDirectory());
    }

    private static SourceStamp CaptureSource()
    {
        // Invocation source stamp, not a claim of archived compiler inputs.
        var commit = ReadTool("git", new[] { "rev-parse", "HEAD" });
        var dirty = ReadTool("git", new[] { "status", "--porcelain" }).Length != 0;
        var sdk = ReadTool("dotnet", new[] { "--version" });
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        return new(commit, dirty, configuration, "SDK " + sdk + "; runtime " + Environment.Version);
    }

    private static string ReadTool(string tool, IEnumerable<string> arguments)
    {
        var info = new ProcessStartInfo(tool) { UseShellExecute = false, RedirectStandardOutput = true,
            RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Cannot read source provenance.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(2000))
        { process.Kill(); process.WaitForExit(); throw new TimeoutException("Provenance tool timeout."); }
        var text = output.GetAwaiter().GetResult().Trim();
        _ = error.GetAwaiter().GetResult();
        if (process.ExitCode != 0 || text.Length > 262144) throw new InvalidOperationException("Source provenance unavailable or oversized.");
        return text;
    }
}
