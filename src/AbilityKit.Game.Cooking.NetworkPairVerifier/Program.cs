using System.Text.Json;
using AbilityKit.Game.Cooking.NetworkPairVerifier;
using AbilityKit.Game.Cooking.RichEvidence;
using AbilityKit.Game.Cooking.Session;

string Option(string key, string fallback = "") { var index = Array.IndexOf(args, key); return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback; }
var hostPath = Path.GetFullPath(Option("--host")); var clientPath = Path.GetFullPath(Option("--client"));
var output = Path.GetFullPath(Option("--output", "pair-verification.json"));
var suite = Option("--suite"); var runId = Option("--run-id"); var caseId = Option("--case");
var hashes = new Dictionary<string,string>(); string? instance = null; var safeOutput = false;
try {
    var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    var paths = new List<string> { hostPath, clientPath, Path.GetFullPath(Option("--host-exit")), Path.GetFullPath(Option("--client-exit")), Path.GetFullPath(Option("--manifest")) };
    if (!string.IsNullOrEmpty(Option("--attestation"))) paths.Add(Path.GetFullPath(Option("--attestation")));
    Check.That(!hostPath.Equals(clientPath,comparison) && !string.IsNullOrWhiteSpace(runId), "Explicit distinct original roles/run ID.");
    foreach (var root in new[] { Path.GetDirectoryName(hostPath)!, Path.GetDirectoryName(clientPath)! }) Check.That(!output.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, comparison), "Output must be outside both original artifact roots.");
    Check.That(paths.All(p => !p.Equals(output,comparison)), "Output cannot overwrite any original receipt/manifest/attestation/input.");
    BoundedJson.SafePath(output); safeOutput = true; long externalBytes = paths.Skip(2).Sum(p => new FileInfo(p).Length); Check.That(externalBytes <= 16L*1024*1024, "All external artifact metadata16MiB aggregate.");
    foreach (var path in paths.Distinct()) hashes.Add(path, BoundedJson.HashBounded(path, path == hostPath || path == clientPath ? BoundedJson.FileLimit : 4*1024*1024, out _));
    Check.That(hashes[hostPath] != hashes[clientPath], "Hardlinked/duplicated role artifact rejected.");
    Check.That(new FileInfo(hostPath).Length + new FileInfo(clientPath).Length <= 256L * 1024 * 1024, "Actual paired256MiB admission bound.");
    using var host = BoundedJson.Read(hostPath); using var client = BoundedJson.Read(clientPath);
    using var hostExit = BoundedJson.Read(Option("--host-exit"), 4*1024*1024); using var clientExit = BoundedJson.Read(Option("--client-exit"), 4*1024*1024);
    using var manifest = BoundedJson.Read(Option("--manifest"), 4*1024*1024);
    var hr = host.RootElement; var cr = client.RootElement;
    var rich = suite == "rich-recovery4-single";
    if (suite == "rich-service") throw new InvalidOperationException("NOT_VERIFIED: historical rich-service export lacks complete cut/ended/successor/final graph evidence; no synthetic reconstruction.");
    Check.That(rich || suite == "concurrency21", "Supported explicitly selected suite.");
    void Receipt(JsonElement exit, JsonElement report, string role, string originalPath)
    {
        Check.That(exit.GetProperty("exitCode").GetInt32() == 0 && exit.GetProperty("role").GetString() == role && exit.GetProperty("runId").GetString() == runId && exit.GetProperty("pid").GetInt32() == (rich ? report.GetProperty("process").GetProperty("pid").GetInt32() : report.GetProperty("pid").GetInt32()) && exit.GetProperty("reportSha256").GetString() == hashes[originalPath], "Actual supervisor exit/PID/run/report-hash receipt for " + role);
        Check.That(DateTimeOffset.TryParse(exit.GetProperty("startUtc").GetString(), out var started) && started < DateTimeOffset.UtcNow.AddMinutes(1), "Actual process start receipt.");
        if (rich) {
            Check.That(exit.GetProperty("caseId").GetString() == caseId && exit.GetProperty("executableSha256").GetString() == report.GetProperty("process").GetProperty("executableSha256").GetString(), "Actual compiled executable/case receipt.");
            var reported = report.GetProperty("process").GetProperty("startUtc").GetDateTimeOffset();
            Check.That(reported >= started && reported - started < TimeSpan.FromSeconds(20), "Actual process start precedes managed entry, no synthetic exact equality.");
        }
    }
    Receipt(hostExit.RootElement, hr, "host", hostPath); Receipt(clientExit.RootElement, cr, "client", clientPath);
    Check.That(manifest.RootElement.GetProperty("schema").GetInt32() == 2 && manifest.RootElement.GetProperty("buildReceipts").EnumerateArray().Count() == 3 && manifest.RootElement.GetProperty("buildReceipts").EnumerateArray().All(x => x.GetProperty("exitCode").GetInt32() == 0 && x.GetProperty("pid").GetInt32() > 0) && manifest.RootElement.GetProperty("buildSucceeded").GetBoolean(), "Original actual successful compile manifest required.");
    if (rich) {
        var provenance = hr.GetProperty("provenance");
        long canonicalBytes = 0; var uniqueCanonical = new HashSet<string>(); var canonicalReferences=0;
        foreach (var endpoint in new[] { (Report:hr,Path:hostPath), (Report:cr,Path:clientPath) }) {
            var files = endpoint.Report.GetProperty("canonicalFiles").EnumerateArray().ToArray(); Check.That((canonicalReferences += files.Length) <= 256, "NOT_VERIFIED: paired canonical reference count256.");
            var root = Path.GetDirectoryName(endpoint.Path)!;
            foreach (var file in files) {
                var relative = file.GetProperty("relativePath").GetString()!; var path = Path.GetFullPath(Path.Combine(root,relative));
                Check.That(path.StartsWith(Path.Combine(root,"canonical") + Path.DirectorySeparatorChar, comparison) && new FileInfo(path).Length <= 4*1024*1024 , "Canonical contained4MiB individual/128MiB paired aggregate admission.");
                var originalHash = BoundedJson.HashBounded(path,4*1024*1024,out var readBytes); Check.That((canonicalBytes += readBytes) <= 128L*1024*1024 && (uniqueCanonical.Add(originalHash) ? uniqueCanonical.Count <= 128 : true), "NOT_VERIFIED: canonical actual paired128MiB/128unique content bound."); hashes.TryAdd(path,originalHash); Check.That(hashes[path] == file.GetProperty("sha256").GetString(), "Original canonical sidefile SHA.");
            }
            var report = endpoint.Report.Deserialize<RichEndpointReport>(CookingNetworkWireCodec.JsonOptions)!;
            var images = report.Phases.Select(x => x.Capture!).Concat(report.Cut!.Snapshots).Concat(new[] { report.Ended?.State,report.Successor?.State }.Where(x => x is not null).Select(x => x!));
            foreach (var capture in images) {
                Check.That(files.Any(x => x.GetProperty("relativePath").GetString() == "canonical/"+capture.RecipeCanonicalHash+".recipe.txt" && x.GetProperty("sha256").GetString() == capture.RecipeCanonicalHash), "Recipe canonical sidefile matches independently recomputed complete graph.");
                if(capture.FrontCanonicalHash is not null) Check.That(files.Any(x => x.GetProperty("relativePath").GetString() == "canonical/"+capture.FrontCanonicalHash+".front.txt" && x.GetProperty("sha256").GetString() == capture.FrontCanonicalHash), "Front canonical sidefile matches complete graph.");
            }
        }
        Check.Equal(manifest.RootElement.GetProperty("files"), provenance.GetProperty("files"), "Exact full frozen runner/config/helper file set.");
        Check.That(manifest.RootElement.GetProperty("sourceHead").GetString() == provenance.GetProperty("sourceHead").GetString() && manifest.RootElement.GetProperty("dirty").GetString() == provenance.GetProperty("dirtyState").GetString(), "Original compiled source labels, not current HEAD relabel.");
        instance = RichPairProof.Verify(hr, cr, runId, caseId);
        // Retained durable payloads are independently hashed, with explicit path containment.
        var root = Path.GetDirectoryName(hostPath)!; var checkpointRoot = Path.Combine(root,"checkpoint");
        var stores = hr.GetProperty("successor").GetProperty("storeFiles").EnumerateArray().ToArray(); Check.That(stores.Length == 1 && stores[0].GetProperty("relativePath").GetString() == "major.checkpoint.json", "Unique exact supported preserved major.checkpoint.json manifest entry required.");
        foreach (var file in stores) {
            var relative = file.GetProperty("relativePath").GetString()!; var path = Path.GetFullPath(Path.Combine(checkpointRoot,relative));
            Check.That(path.StartsWith(Path.GetFullPath(checkpointRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && new FileInfo(path).Length <= 4*1024*1024 && new FileInfo(path).LinkTarget is null, "Contained separately bounded durable checkpoint.");
            for (FileSystemInfo? entry = new FileInfo(path); entry is not null; entry = entry is FileInfo f ? f.Directory : ((DirectoryInfo)entry).Parent) Check.That(entry.LinkTarget is null, "No junction/symlink durable input ancestors.");
            Check.That((externalBytes += new FileInfo(path).Length) <= 16L*1024*1024, "All external metadata/durable files16MiB cumulative bound.");
            hashes.TryAdd(path,BoundedJson.Hash(path));
            Check.That(hashes[path] == file.GetProperty("sha256").GetString(), "Actual retained durable store file SHA.");
        }
        var exactStore = Path.Combine(checkpointRoot,"major.checkpoint.json"); BoundedJson.SafePath(exactStore);
        using var durableGuard = new FileStream(exactStore,FileMode.Open,FileAccess.Read,FileShare.Read);
        using var independentlyBoundedStore = BoundedJson.Read(exactStore,4*1024*1024);
        Check.That(hashes.ContainsKey(exactStore), "Exact store raw origin hash was admitted before public read.");
        var read = new CookingMajorCheckpointStore(checkpointRoot).ReadBaseline(hr.GetProperty("successor").GetProperty("source").Deserialize<CookingLevelScope>(CookingNetworkWireCodec.JsonOptions)!.MatchScope);
        Check.That(read.Accepted && read.Payload is not null, "Independent actual readonly durable Decode/Integrity/ReadBaseline accepted.");
        Check.Equal(JsonSerializer.SerializeToElement(read.Payload, CookingNetworkWireCodec.JsonOptions), hr.GetProperty("successor").GetProperty("savedPayload"), "Reported actual read payload equals retained independently decoded bytes.");
    } else {
        var frozen = manifest.RootElement.GetProperty("controlFiles").EnumerateArray().ToDictionary(x => x.GetProperty("relativePath").GetString()!,x => x.GetProperty("sha256").GetString()!);
        Check.Equal(JsonSerializer.SerializeToElement(frozen),hr.GetProperty("binaries"), "Exact original audited unchanged concurrency compile binaries.");
        Check.That(manifest.RootElement.GetProperty("sourceHead").GetString() == hr.GetProperty("source").GetString(), "Actual original controls compile source.");
        instance = ConcurrencyPairProof.Verify(hr,cr,runId);
    }
    var physical = "NOT_VERIFIED";
    var attestationPath = Option("--attestation");
    if (!string.IsNullOrEmpty(attestationPath)) {
        using var attestation = BoundedJson.Read(attestationPath,1024*1024); var a = attestation.RootElement;
        Check.That(a.GetProperty("attested").GetBoolean() && a.GetProperty("runId").GetString() == runId && !string.IsNullOrWhiteSpace(a.GetProperty("operator").GetString()) && a.GetProperty("hostMachine").GetString() != a.GetProperty("clientMachine").GetString(), "Explicit actual distinct physical PC attestation.");
        Check.That(a.GetProperty("hostMachine").GetString() == hostExit.RootElement.GetProperty("machine").GetString() && a.GetProperty("clientMachine").GetString() == clientExit.RootElement.GetProperty("machine").GetString(), "Attested endpoints match actual local supervisor machines.");
        var ip = System.Net.IPAddress.Parse(a.GetProperty("hostIp").GetString()!); Check.That(!System.Net.IPAddress.IsLoopback(ip) && !ip.Equals(System.Net.IPAddress.Any), "Actual attested LAN Host address.");
        Check.That(hr.GetProperty("topology").GetString() == "SeparateHostsRequiresPairedEvidence" && cr.GetProperty("topology").GetString() == "SeparateHostsRequiresPairedEvidence", "Separate physical endpoint deployment claim.");
        var endpoint = rich ? cr.GetProperty("endpoint").GetString()! : cr.GetProperty("address").GetString()!;
        var target = rich ? System.Net.IPEndPoint.Parse(endpoint) : new System.Net.IPEndPoint(System.Net.IPAddress.Parse(endpoint), cr.GetProperty("port").GetInt32());
        Check.That(target.Address.Equals(ip), "Exact remote LAN address, no string-prefix match.");
        var stdoutPath = Path.Combine(Path.GetDirectoryName(hostPath)!, "host.stdout.log"); Check.That(new FileInfo(stdoutPath).Length <= 4*1024*1024 && (externalBytes += new FileInfo(stdoutPath).Length) <= 16L*1024*1024, "READY stdout4MiB bound."); hashes.TryAdd(stdoutPath,BoundedJson.Hash(stdoutPath));
        var ready = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(stdoutPath), @"(?m)^READY ([0-9]+) ([0-9]+)\s*$");
        Check.That(ready.Success && int.Parse(ready.Groups[1].Value) == target.Port && int.Parse(ready.Groups[2].Value) == hostExit.RootElement.GetProperty("pid").GetInt32(), "Actual READY exact UDP port/PID reconciliation.");
        physical = "TwoPhysicalPcAttestedPairedEvidence";
    }
    foreach (var item in hashes) Check.That(BoundedJson.HashBounded(item.Key, item.Key == hostPath || item.Key == clientPath ? BoundedJson.FileLimit : 4*1024*1024, out _) == item.Value, "Original immutable report hash after verification.");
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    File.WriteAllText(output, JsonSerializer.Serialize(new { passed=true,suite,runId,caseId,serverInstance=instance,physicalTwoPc=physical,formalPerformanceTarget="UNSET",originalHashes=hashes,artifactBytes=new FileInfo(hostPath).Length+new FileInfo(clientPath).Length,coverage="complete declared case; four-case aggregate requires distinct four pairs" }, new JsonSerializerOptions{WriteIndented=true}));
    return 0;
} catch (Exception error) {
    if (!safeOutput) { Console.Error.WriteLine(error); return 1; }
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    File.WriteAllText(output, JsonSerializer.Serialize(new { passed=false,suite,runId,caseId,status=error.Message.Contains("NOT_VERIFIED")?"NOT_VERIFIED":"FAILED",failure=error.ToString(),physicalTwoPc="NOT_VERIFIED",originalHashes=hashes }, new JsonSerializerOptions{WriteIndented=true}));
    Console.Error.WriteLine(error); return 1;
}
