using System.Text.Json;
using AbilityKit.Game.Cooking.Session;
namespace AbilityKit.Game.Cooking.NetworkPairVerifier;

// Frozen suite identities only. This verifier never constructs a fixture/authority/transport.
internal static class ControlEvidence
{
    public static readonly PlayerId Chef = new("control-chef"), Partner = new("control-partner");
    public static readonly StationSlotId Slot = new("single-slot");
    public static readonly ItemId Shared = new("shared"), ChefFood = new("chef-food"), PartnerFood = new("partner-food"), Good = new("compatible"), Vessel = new("vessel");
    public static readonly ItemId[] ItemIds = [Shared, ChefFood, PartnerFood, new("incompatible"), Good, Vessel];
    public static CookingRecipeCheckpointItem Item(CookingNetworkAuthorityCapture capture, ItemId id) => capture.FullRecipe!.Items.Single(x => x.Id == id);
    public static void Require(bool value, string message) => Check.That(value, message);
}

internal static class ConcurrencyPairProof
{
    private static T Read<T>(JsonElement value) => value.Deserialize<T>(CookingNetworkWireCodec.JsonOptions)!;
    public static string Verify(JsonElement host, JsonElement client, string runId)
    {
        Check.That(host.GetProperty("fixture").GetString() == "real-et-spatial-concurrency-v1" && client.GetProperty("fixture").GetString() == "real-et-spatial-concurrency-v1", "Supported actual spatial suite.");
        foreach (var field in new[] { "runId", "source", "dirty", "fixture", "protocol", "checkpointFormat", "order", "topology", "etMvid", "sessionMvid", "binaries" }) Check.Equal(host.GetProperty(field), client.GetProperty(field), "Paired frozen provenance: " + field);
        Check.That(host.GetProperty("runId").GetString() == runId && host.GetProperty("role").GetString() == "host" && client.GetProperty("role").GetString() == "client" && host.GetProperty("passed").GetBoolean() && client.GetProperty("passed").GetBoolean() && host.GetProperty("protocol").GetInt32() == 3 && host.GetProperty("checkpointFormat").GetInt32() == CookingLevelCheckpointCodec.CurrentFormatVersion && Guid.Parse(host.GetProperty("etMvid").GetString()!) != Guid.Empty && Guid.Parse(host.GetProperty("sessionMvid").GetString()!) != Guid.Empty, "Actual successful role/run/current protocol/MVIDs.");
        var hs = host.GetProperty("evidence").EnumerateArray().ToArray(); var cs = client.GetProperty("evidence").EnumerateArray().ToArray();
        Check.That(hs.Length == 21 && cs.Length == 21 && hs.Select(x => x.GetProperty("phase").GetInt32()).Order().SequenceEqual(Enumerable.Range(1,21)) && cs.Select(x => x.GetProperty("phase").GetInt32()).Order().SequenceEqual(Enumerable.Range(1,21)), "Exactly21 unique mandatory phases.");
        for (var phase = 1; phase <= 21; phase++) {
            var h = hs.Single(x => x.GetProperty("phase").GetInt32() == phase); var c = cs.Single(x => x.GetProperty("phase").GetInt32() == phase);
            Check.Equal(h.GetProperty("delivered").GetProperty("command"), c.GetProperty("wire"), "Actual sent/observed remote wire phase" + phase);
            Check.Equal(h.GetProperty("remote"), c.GetProperty("result"), "Actual outbound/received terminal phase" + phase);
            var before = Read<CookingNetworkAuthorityCapture>(h.GetProperty("before")); var after = Read<CookingNetworkAuthorityCapture>(h.GetProperty("after")); StateProof.Full(before); StateProof.Full(after);
            var frame = Read<CookingNetworkOwnerFrameResult>(h.GetProperty("frame")); var local = Read<CookingNetworkWireResult>(h.GetProperty("local")); var remote = Read<CookingNetworkWireResult>(h.GetProperty("remote"));
            ControlProof.Frame(phase, before, after, frame, local, remote);
            StateProof.Baseline(Read<CookingNetworkBaseline>(c.GetProperty("before")));
            if (phase is 20 or 21) {
                var initial = hs.Single(x => x.GetProperty("phase").GetInt32() == (phase == 20 ? 1 : 4));
                foreach (var actor in new[] { "local", "remote" }) {
                    var original = Read<CookingNetworkWireResult>(initial.GetProperty(actor)); var cached = Read<CookingNetworkWireResult>(h.GetProperty(actor));
                    Check.That(cached.DomainCommandId == original.DomainCommandId && cached.Result!.Outcome == original.Result!.Outcome && cached.Result.Reason == original.Result.Reason && cached.Result.StateVersion == original.Result.StateVersion && cached.Result.IsDuplicate, "Both original contested winner/loser cached unchanged.");
                }
            }
            if (phase == 14) {
                var wire = Read<CookingNetworkWireCommand>(c.GetProperty("wire"));
                foreach (var invalid in new[] { 11,12 }) Check.That(wire.ClientSequence < Read<CookingNetworkWireCommand>(cs.Single(x => x.GetProperty("phase").GetInt32() == invalid).GetProperty("wire")).ClientSequence, "Wrong guard high sequence cannot poison lower legal input.");
            }
        }
        var hf = host.GetProperty("final"); var cf = client.GetProperty("final");
        var capture = Read<CookingNetworkAuthorityCapture>(hf.GetProperty("capture")); StateProof.Full(capture);
        var localBaseline = Read<CookingNetworkBaseline>(hf.GetProperty("localBaseline")); var remoteBaseline = Read<CookingNetworkBaseline>(cf.GetProperty("remoteBaseline")); StateProof.Baseline(localBaseline); StateProof.Baseline(remoteBaseline);
        Check.That(CookingNetworkWireCodec.Hash(capture) == hf.GetProperty("hash").GetString() && hf.GetProperty("hash").GetString() == cf.GetProperty("hash").GetString() && CookingNetworkWireCodec.Hash(remoteBaseline.State) == hf.GetProperty("hash").GetString(), "Recomputed deep final gameplay consensus.");
        Check.Equal(hf.GetProperty("scope"), cf.GetProperty("scope"), "Exact scope."); Check.Equal(hf.GetProperty("serverSessionInstance"), cf.GetProperty("serverSessionInstance"), "Exact authority instance.");
        var projection = Read<CookingNetworkSessionProjection>(hf.GetProperty("preCloseProjection"));
        Check.That(projection.Participants.Count == 2 && projection.Participants.All(x => x.ConnectedOwnerBinding && x.Ready && !x.CleanupPending) && remoteBaseline.Session.Participants.All(x => x.ConnectedOwnerBinding && x.Ready && !x.CleanupPending), "Immutable paired live pre-close both Ready/noCleanup.");
        var issued = Read<CookingNetworkBaselineIdentity>(hf.GetProperty("remoteIssued").GetProperty("identity")); Check.Equal(issued, remoteBaseline.Identity, "Exact newest remote issued image.");
        var channel = hf.GetProperty("remoteIssued").GetProperty("channel").GetString();
        Check.That(hf.GetProperty("ingress").EnumerateArray().Any(x => x.GetProperty("channel").GetString() == channel && x.GetProperty("kind").GetString() == "BaselineAck" && Check.Canonical(x.GetProperty("ack")) == Check.Canonical(JsonSerializer.SerializeToElement(issued, CookingNetworkWireCodec.JsonOptions))), "Actual exact ACK/current channel.");
        Check.That(hf.GetProperty("remoteCurrentExactAckReady").GetBoolean() && hf.GetProperty("localExactAckReady").GetBoolean() && cf.GetProperty("remoteExactAckReady").GetBoolean() && cf.GetProperty("generation").GetInt64() == 2, "Actual newest rebound Ready grants.");
        return hf.GetProperty("serverSessionInstance").GetString()!;
    }
}
