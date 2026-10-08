using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using AbilityKit.Game.Cooking.FlowAcceptance;
using Xunit;

namespace AbilityKit.ET.Runtime.Tests;

public sealed class CookingNetworkTestPortTests
{
    private static readonly Type Selector = typeof(NetworkFlowAdapter).Assembly
        .GetType("AbilityKit.Game.Cooking.FlowAcceptance.CookingTestPortSelector")!;
    private static object? Invoke(string method, params object?[] args) => Selector
        .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, args);
    private static JsonObject Response() => JsonSerializer.SerializeToNode(new
    {
        action = "GetPort", status = "Passed", localPolicy = "NotRun", remoteConnectivity = "NotRun",
        firewallMutationAttempted = false, portReserved = false, port = 18093,
        configPath = Path.Combine(Path.GetTempPath(), "cooking-test-ports.json"), configSource = "SavedConfig",
        config = new { schemaVersion = 1, startPort = 18090, endPort = 18099, protocol = "UDP",
            profiles = new[] { "Private", "Public" }, remoteAddress = "LocalSubnet", ruleName = "AbilityKit.Cooking.TestPorts" }
    })!.AsObject();

    [Fact]
    public void Validated_selector_result_preserves_exact_saved_range_and_unreserved_provenance()
    {
        var selection = Invoke("ValidateResponse", Response().ToJsonString(), 0, 123, "selector.ps1")!;
        Assert.Equal(18093, selection.GetType().GetProperty("Port")!.GetValue(selection));
        Assert.Equal("SavedConfig", selection.GetType().GetProperty("ConfigSource")!.GetValue(selection));
        Assert.Equal(18090, selection.GetType().GetProperty("StartPort")!.GetValue(selection));
        Assert.Equal(18099, selection.GetType().GetProperty("EndPort")!.GetValue(selection));
        Assert.Equal(false, selection.GetType().GetProperty("PortReserved")!.GetValue(selection));
    }

    [Theory]
    [InlineData("failed")]
    [InlineData("action")]
    [InlineData("string-port")]
    [InlineData("fractional-port")]
    [InlineData("outside-range")]
    [InlineData("unknown-config")]
    [InlineData("schema")]
    [InlineData("range-string")]
    [InlineData("range-reversed")]
    [InlineData("protocol")]
    [InlineData("profiles")]
    [InlineData("address")]
    [InlineData("rule")]
    [InlineData("reserved")]
    [InlineData("mutation")]
    [InlineData("connectivity")]
    [InlineData("relative-config")]
    [InlineData("source")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("malformed")]
    public void Malformed_failed_or_out_of_policy_selector_results_are_rejected(string fault)
    {
        var value = Response();
        var config = value["config"]!.AsObject();
        switch (fault)
        {
            case "failed": value["status"] = "Failed"; break;
            case "action": value["action"] = "Show"; break;
            case "string-port": value["port"] = "18093"; break;
            case "fractional-port": value["port"] = 18093.5; break;
            case "outside-range": value["port"] = 18100; break;
            case "unknown-config": config["extra"] = true; break;
            case "schema": config["schemaVersion"] = true; break;
            case "range-string": config["startPort"] = "18090"; break;
            case "range-reversed": config["endPort"] = 18089; break;
            case "protocol": config["protocol"] = "TCP"; break;
            case "profiles": config["profiles"] = new JsonArray("Private", "Private"); break;
            case "address": config["remoteAddress"] = "Any"; break;
            case "rule": config["ruleName"] = "other"; break;
            case "reserved": value["portReserved"] = true; break;
            case "mutation": value["firewallMutationAttempted"] = true; break;
            case "connectivity": value["remoteConnectivity"] = "Passed"; break;
            case "relative-config": value["configPath"] = "ports.json"; break;
            case "source": value["configSource"] = "Invented"; break;
            case "missing": value.Remove("port"); break;
        }
        var text = fault == "malformed" ? "{bad" : value.ToJsonString();
        if (fault == "duplicate") text = text.Replace("\"port\":18093", "\"port\":18093,\"port\":18094");
        Assert.Throws<TargetInvocationException>(() => Invoke("ValidateResponse", text, 0, 123, "selector.ps1"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("-1")]
    [InlineData(" 18090")]
    [InlineData("18090.0")]
    [InlineData("18090junk")]
    public void Windows_child_refuses_missing_or_invalid_selected_port(string? value) =>
        Assert.Throws<TargetInvocationException>(() => Invoke("ReadChildPort", value, true));

    [Fact]
    public void Windows_child_consumes_selected_integer_and_nonwindows_keeps_os_ephemeral()
    {
        Assert.Equal(18093, Invoke("ReadChildPort", "18093", true));
        Assert.Equal(0, Invoke("ReadChildPort", "invalid-inherited-value", false));
        Invoke("ValidateReadyEndpoint", "127.0.0.1:18093", 18093);
        Invoke("ValidateReadyEndpoint", "127.0.0.1:45678", 0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("127.0.0.1:18094")]
    [InlineData("192.168.1.10:18093")]
    [InlineData("127.0.0.1:0")]
    [InlineData("invalid")]
    public void Ready_endpoint_mismatch_is_rejected_before_clients(string? endpoint) =>
        Assert.Throws<TargetInvocationException>(() => Invoke("ValidateReadyEndpoint", endpoint, 18093));

    [Fact]
    public void Installed_selector_precedes_repository_fallback_and_missing_tool_is_explicit()
    {
        using var files = new TemporaryFiles();
        var tool = Path.Combine(files.Root, "repo", "tools", "cooking-firewall.ps1");
        var root = Path.Combine(files.Root, "repo", "src", "bin");
        Directory.CreateDirectory(Path.GetDirectoryName(tool)!); Directory.CreateDirectory(root);
        File.WriteAllText(tool, "repo fixture");
        var local = Path.Combine(files.Root, "local");
        Assert.Equal(tool, Invoke("ResolveScript", local, new[] { root }));
        var installed = Path.Combine(local, "AbilityKit", "CookingNetwork", "bin", "cooking-firewall.ps1");
        Directory.CreateDirectory(Path.GetDirectoryName(installed)!); File.WriteAllText(installed, "installed fixture");
        Assert.Equal(installed, Invoke("ResolveScript", local, new[] { root }));
        File.Delete(installed); File.Delete(tool);
        Assert.Throws<TargetInvocationException>(() => Invoke("ResolveScript", local, Array.Empty<string>()));
    }

    [Theory]
    [InlineData("native")]
    [InlineData("malformed")]
    [InlineData("oversized")]
    public async Task Selector_process_failures_cannot_become_successful_candidates(string fault)
    {
        var output = fault == "malformed" ? "bad-json" : fault == "oversized" ? new string('x', 65537) : Response().ToJsonString();
        using var budget = new CancellationTokenSource(10000);
        var task = (Task)Invoke("ExecuteAsync", WriteCommand(output, fault == "native" ? 7 : 0), "fixture-selector", budget.Token)!;
        await Assert.ThrowsAnyAsync<Exception>(async () => await task);
    }

    [Fact]
    public async Task Native_selector_failure_retains_actionable_bounded_error()
    {
        var output = Response(); output["status"] = "Failed"; output["error"] = "No unoccupied UDP port was found in the configured range.";
        using var budget = new CancellationTokenSource(10000);
        var task = (Task)Invoke("ExecuteAsync", WriteCommand(output.ToJsonString(), 1), "fixture-selector", budget.Token)!;
        var error = await Assert.ThrowsAsync<IOException>(async () => await task);
        Assert.Contains("NativeFailure:1:No unoccupied UDP port", error.Message);
    }

    [Fact]
    public async Task Successful_selector_process_retains_script_response_and_native_provenance()
    {
        using var files = new TemporaryFiles();
        var script = Path.Combine(files.Root, "selector-fixture.ps1");
        File.WriteAllText(script, "test fixture identity");
        var response = Response().ToJsonString();
        using var budget = new CancellationTokenSource(10000);
        var task = (Task)Invoke("ExecuteAsync", WriteCommand(response, 0), script, budget.Token)!;
        await task;
        var selection = task.GetType().GetProperty("Result")!.GetValue(task)!;
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(script))), selection.GetType().GetProperty("ScriptSha256")!.GetValue(selection));
        Assert.Equal(0, selection.GetType().GetProperty("SelectorNativeExitCode")!.GetValue(selection));
        Assert.Equal(response, selection.GetType().GetProperty("SelectorResponse")!.GetValue(selection));
        var processId = (int)selection.GetType().GetProperty("SelectorProcessId")!.GetValue(selection)!;
        try { using var process = Process.GetProcessById(processId); Assert.True(process.HasExited); }
        catch (ArgumentException) { /* An exited selector no longer has a live process ID. */ }
    }

    [Fact]
    public async Task Cancellation_terminates_only_owned_selector_and_confirms_native_exit()
    {
        using var files = new TemporaryFiles();
        var receipt = Path.Combine(files.Root, "selector.pid");
        using var unrelated = Process.Start(SleepCommand(null))!;
        using var budget = new CancellationTokenSource(10000);
        Task? task = null;
        try
        {
            task = (Task)Invoke("ExecuteAsync", SleepCommand(receipt), "fixture-selector", budget.Token)!;
            using var ready = new CancellationTokenSource(5000);
            while (!File.Exists(receipt) || new FileInfo(receipt).Length == 0) await Task.Delay(10, ready.Token);
            var ownedId = int.Parse(File.ReadAllText(receipt).Trim());
            using var owned = Process.GetProcessById(ownedId);
            budget.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);
            Assert.True(owned.HasExited);
            Assert.False(unrelated.HasExited);
        }
        finally
        {
            try
            {
                budget.Cancel();
                if (task is not null)
                    try { await task.WaitAsync(TimeSpan.FromSeconds(3)); } catch (OperationCanceledException) { }
            }
            finally
            {
                if (!unrelated.HasExited) unrelated.Kill();
                using var cleanup = new CancellationTokenSource(2000);
                await unrelated.WaitForExitAsync(cleanup.Token);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Selector_failure_or_startup_timeout_launches_no_roles_and_cleanup_stays_complete(bool timeout)
    {
        OnOwner(async () =>
        {
            using var files = new TemporaryFiles();
            var adapter = new NetworkFlowAdapter();
            var field = typeof(NetworkFlowAdapter).GetField("selectPort", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var selectionType = field.FieldType.GenericTypeArguments[1].GenericTypeArguments[0];
            var seam = typeof(CookingNetworkTestPortTests).GetMethod(timeout ? nameof(WaitSelector) : nameof(FailSelector),
                BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(selectionType).CreateDelegate(field.FieldType);
            field.SetValue(adapter, seam);
            var request = FixedFlows.Request("compete-one-item", files.Root);
            if (timeout) request = request with { Budgets = request.Budgets with { StartupMs = 100 } };
            var run = new RunIdentity(request.RequestId, "selector-test", "attempt", 1);
            await Assert.ThrowsAnyAsync<Exception>(async () => await adapter.StartAsync(request, run, new Sink(), default));
            Assert.Empty(((IEnumerable)typeof(NetworkFlowAdapter).GetField("roles", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(adapter)!).Cast<object>());
            var cleanup = await adapter.CloseAsync(3000, default);
            Assert.Equal(CleanupState.Complete, cleanup.State); Assert.Empty(cleanup.Resources);
            using var sidecar = JsonDocument.Parse(File.ReadAllText(Path.Combine(files.Root, "network-resources.json")));
            Assert.Empty(sidecar.RootElement.GetProperty("children").EnumerateArray());
            return 0;
        });
    }

    private static Task<T> FailSelector<T>(CancellationToken token) => Task.FromException<T>(new IOException("ControlledSelectorFailure"));
    private static async Task<T> WaitSelector<T>(CancellationToken token)
    { await Task.Delay(Timeout.Infinite, token); throw new InvalidOperationException("UnreachableSelector"); }

    private static int OnOwner(Func<Task<int>> action) => (int)typeof(NetworkFlowAdapter).Assembly
        .GetType("AbilityKit.Game.Cooking.NetworkRichRecoveryAcceptance.SingleThreadOwner")!
        .GetMethod("Run", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, new object[] { action })!;

    private static ProcessStartInfo WriteCommand(string output, int exitCode)
    {
        if (output.Length > 65536)
            return Shell(OperatingSystem.IsWindows() ? "[Console]::Out.Write('x' * 65537)" : "head -c 65537 /dev/zero | tr '\\0' x");
        var command = OperatingSystem.IsWindows()
            ? "[Console]::Out.Write('" + output.Replace("'", "''") + "'); exit " + exitCode
            : "printf '%s' '" + output.Replace("'", "'\\''") + "'; exit " + exitCode;
        return Shell(command);
    }
    private static ProcessStartInfo SleepCommand(string? receipt)
    {
        var command = OperatingSystem.IsWindows()
            ? (receipt is null ? "" : "[IO.File]::WriteAllText('" + receipt.Replace("'", "''") + "', [string]$PID); ") + "Start-Sleep -Seconds 30"
            : (receipt is null ? "" : "echo $$ > '" + receipt.Replace("'", "'\\''") + "'; ") + "exec sleep 30";
        return Shell(command);
    }
    private static ProcessStartInfo Shell(string command)
    {
        var info = new ProcessStartInfo(OperatingSystem.IsWindows() ? "powershell.exe" : "/bin/sh")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in OperatingSystem.IsWindows() ? new[] { "-NoProfile", "-Command", command } : new[] { "-c", command })
            info.ArgumentList.Add(arg);
        return info;
    }
    private sealed class Sink : IFlowEventSink
    {
        public bool EvidenceComplete => true;
        public string? ErrorCode => null;
        public bool TryPublish(FlowEvent value) => true;
    }
    private sealed class TemporaryFiles : IDisposable
    {
        internal readonly string Root = Path.Combine(Path.GetTempPath(), "cooking-port-tests-" + Guid.NewGuid().ToString("N"));
        internal TemporaryFiles() => Directory.CreateDirectory(Root);
        public void Dispose()
        {
            var full = Path.GetFullPath(Root);
            var prefix = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("cooking-port-tests-", StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected test cleanup path");
            Directory.Delete(full, true);
        }
    }
}
