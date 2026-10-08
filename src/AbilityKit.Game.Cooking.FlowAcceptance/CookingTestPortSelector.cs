using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace AbilityKit.Game.Cooking.FlowAcceptance;

// Private test-launch plumbing; no port lease or product protocol is introduced.
internal sealed record CookingTestPortSelection(int Port, string Mode, string? ScriptPath = null,
    int? SelectorProcessId = null, string? ConfigPath = null, string? ConfigSource = null,
    int? StartPort = null, int? EndPort = null, bool PortReserved = false, string? ScriptSha256 = null,
    int? SelectorNativeExitCode = null, string? SelectorResponse = null, string? SelectorDiagnostics = null);

internal static class CookingTestPortSelector
{
    internal const string PortEnvironmentVariable = "ABILITYKIT_COOKING_FLOW_TEST_PORT";
    private const int MaxOutputCharacters = 65536;

    internal static Task<CookingTestPortSelection> SelectAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows()) return Task.FromResult(new CookingTestPortSelection(0, "OsEphemeral"));
        var script = ResolveScript(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory });
        var info = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var arg in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script, "-Action", "GetPort" })
            info.ArgumentList.Add(arg);
        return ExecuteAsync(info, script, token);
    }

    private static string ResolveScript(string localApplicationData, IEnumerable<string> searchRoots)
    {
        var installed = Path.Combine(localApplicationData, "AbilityKit", "CookingNetwork", "bin", "cooking-firewall.ps1");
        if (File.Exists(installed)) return installed;
        foreach (var root in searchRoots)
            for (var directory = new DirectoryInfo(Path.GetFullPath(root)); directory is not null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, "tools", "cooking-firewall.ps1");
                if (File.Exists(candidate)) return candidate;
            }
        throw new FileNotFoundException("CookingPortSelectorToolMissing: install cooking-firewall.ps1 or run from its repository.");
    }

    private static async Task<CookingTestPortSelection> ExecuteAsync(ProcessStartInfo info, string script, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var scriptHash = File.Exists(script) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(script))) : null;
        using var process = Process.Start(info) ?? throw new IOException("CookingPortSelectorStartFailed");
        using var io = CancellationTokenSource.CreateLinkedTokenSource(token);
        var output = ReadBoundedAsync(process.StandardOutput, io.Token);
        var diagnostics = ReadBoundedAsync(process.StandardError, io.Token);
        try
        {
            await Task.WhenAll(output, diagnostics, process.WaitForExitAsync(io.Token));
            token.ThrowIfCancellationRequested();
            var response = await output; var diagnostic = await diagnostics;
            if (process.ExitCode != 0)
                throw new IOException("CookingPortSelectorNativeFailure:" + process.ExitCode + ":" + FailureDetail(response, diagnostic));
            return ValidateResponse(response, process.ExitCode, process.Id, script) with
            { ScriptSha256 = scriptHash, SelectorNativeExitCode = process.ExitCode, SelectorResponse = response, SelectorDiagnostics = diagnostic };
        }
        finally
        {
            io.Cancel();
            // Only this owned selector handle is terminated, including cancellation and output failures.
            var mayBeAlive = true;
            try { mayBeAlive = !process.HasExited; } catch (InvalidOperationException) { }
            if (mayBeAlive)
                try { process.Kill(); } catch (InvalidOperationException) when (process.HasExited) { }
            using var cleanup = new CancellationTokenSource(2000);
            await process.WaitForExitAsync(cleanup.Token);
            try { await Task.WhenAll(output, diagnostics).WaitAsync(cleanup.Token); }
            catch (OperationCanceledException) when (io.IsCancellationRequested) { }
            catch (InvalidDataException) { /* The bounded reader fault is propagated by the main await. */ }
        }
    }

    private static string FailureDetail(string output, string diagnostics)
    {
        var detail = output;
        try
        {
            using var json = JsonDocument.Parse(output);
            if (json.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
                detail = error.GetString() ?? output;
        }
        catch (JsonException) { }
        catch (InvalidOperationException) { }
        if (detail.Length > 4096) detail = detail[..4096];
        if (diagnostics.Length > 4096) diagnostics = diagnostics[..4096];
        return detail + (string.IsNullOrEmpty(diagnostics) ? "" : ";stderr=" + diagnostics);
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationToken token)
    {
        var value = new System.Text.StringBuilder();
        var buffer = new char[4096];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), token)) != 0)
        {
            if (value.Length + count > MaxOutputCharacters) throw new InvalidDataException("CookingPortSelectorOutputTooLarge");
            value.Append(buffer, 0, count);
        }
        return value.ToString();
    }

    private static CookingTestPortSelection ValidateResponse(string output, int exitCode, int processId, string script)
    {
        if (exitCode != 0) throw new IOException("CookingPortSelectorNativeFailure:" + exitCode);
        using var json = JsonDocument.Parse(output);
        var response = json.RootElement;
        RequireObject(response);
        if (String(response, "action") != "GetPort" || String(response, "status") != "Passed" ||
            String(response, "localPolicy") != "NotRun" || String(response, "remoteConnectivity") != "NotRun" ||
            response.GetProperty("portReserved").ValueKind != JsonValueKind.False ||
            response.GetProperty("firewallMutationAttempted").ValueKind != JsonValueKind.False)
            throw new InvalidDataException("CookingPortSelectorResultContract");
        var config = response.GetProperty("config");
        RequireObject(config);
        var names = config.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray();
        if (!names.SequenceEqual(new[] { "endPort", "profiles", "protocol", "remoteAddress", "ruleName", "schemaVersion", "startPort" }) ||
            Integer(config, "schemaVersion") != 1 || String(config, "protocol") != "UDP" ||
            String(config, "remoteAddress") != "LocalSubnet" || String(config, "ruleName") != "AbilityKit.Cooking.TestPorts")
            throw new InvalidDataException("CookingPortSelectorConfigContract");
        var profiles = config.GetProperty("profiles");
        if (profiles.ValueKind != JsonValueKind.Array || profiles.GetArrayLength() != 2 ||
            !profiles.EnumerateArray().Select(p => p.ValueKind == JsonValueKind.String ? p.GetString() : null)
                .Order(StringComparer.Ordinal).SequenceEqual(new[] { "Private", "Public" }))
            throw new InvalidDataException("CookingPortSelectorProfiles");
        var start = Integer(config, "startPort"); var end = Integer(config, "endPort"); var port = Integer(response, "port");
        if (start < 1 || end > 65535 || start > end || port < start || port > end)
            throw new InvalidDataException("CookingPortSelectorRange");
        var configPath = String(response, "configPath"); var source = String(response, "configSource");
        if (!Path.IsPathFullyQualified(configPath) || source is not ("SavedConfig" or "BundledDefaults" or "ExplicitRange"))
            throw new InvalidDataException("CookingPortSelectorProvenance");
        return new(port, "CookingGetPort", script, processId, configPath, source, start, end);
    }

    private static void RequireObject(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object ||
            value.EnumerateObject().GroupBy(p => p.Name, StringComparer.Ordinal).Any(g => g.Count() != 1))
            throw new InvalidDataException("CookingPortSelectorObjectOrDuplicateField");
    }
    private static string String(JsonElement value, string name) => value.GetProperty(name).ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetProperty(name).GetString()) ? value.GetProperty(name).GetString()! :
        throw new InvalidDataException("CookingPortSelectorString:" + name);
    private static int Integer(JsonElement value, string name)
    {
        var field = value.GetProperty(name);
        if (field.ValueKind != JsonValueKind.Number || !field.TryGetInt32(out var result) ||
            !field.GetRawText().All(char.IsAsciiDigit)) throw new InvalidDataException("CookingPortSelectorInteger:" + name);
        return result;
    }

    internal static int ReadChildPort(string? value, bool windows)
    {
        if (!windows) return 0;
        if (string.IsNullOrEmpty(value) || !value.All(char.IsAsciiDigit) ||
            !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port is < 1 or > 65535)
            throw new InvalidDataException("CookingSelectedPortEnvironmentInvalid");
        return port;
    }

    internal static void ValidateReadyEndpoint(string? endpoint, int selectedPort)
    {
        if (!FlowRoleProtocol.TryEndpoint(endpoint, out var bound) || !bound.Address.Equals(IPAddress.Loopback) ||
            selectedPort != 0 && bound.Port != selectedPort)
            throw new InvalidDataException("CookingSelectedPortReadyMismatch");
    }
}
