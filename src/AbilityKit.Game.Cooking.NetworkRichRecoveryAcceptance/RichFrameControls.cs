using System.Buffers.Binary;
using System.Text.Json;
using AbilityKit.Game.Cooking.Session;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Game.Cooking.NetworkRichRecoveryAcceptance;

internal static class RichFrameControls
{
    public static int Run()
    {
        var passed = new List<string>();
        try {
            var codec = new RichFrameCodec();
            var limit = RichFrameCodec.MaximumBodyBytes;
            foreach (var body in new[] { 4194973, limit }) {
                var payload = new byte[body - NetworkPacketHeader.Size];
                payload[0] = 17; payload[^1] = 29;
                var header = new NetworkPacketHeader(default, CookingNetworkWireCodec.OpCode, 7, (uint)payload.Length);
                var frame = codec.Encode(header, new(payload));
                Require(frame.Count == body + 4, "Prefix/body length");
                var decoder = codec.CreateDecoder(); var offset = 0;
                foreach (var size in new[] { 1, 2, 1, 3, 13, frame.Count - 20 }) {
                    decoder.Append(new(frame.Array!, frame.Offset + offset, size)); offset += size;
                    var ready = decoder.TryRead(out var actualHeader, out var actualPayload);
                    Require(ready == (offset == frame.Count), "Fragment completion");
                    if (ready) Require(actualHeader.Seq == 7 && actualHeader.PayloadLength == payload.Length && actualPayload.AsSpan().SequenceEqual(payload), "Exact decoded bytes");
                }
                Require(!decoder.TryRead(out _, out _), "No duplicate frame");
                decoder.Reset(); Require(!decoder.TryRead(out _, out _), "Reset empty");
                passed.Add(body == limit ? "configured-body-exact-fragmented" : "original-red-body-fragmented");
            }
            RejectPrefix(codec.CreateDecoder(), limit + 1); passed.Add("configured-body-plus-one-rejected");
            RejectPrefix(LengthPrefixedFrameCodec.Instance.CreateDecoder(), 4194973); passed.Add("generic-default-4MiB-unchanged");
            var bounds = new CookingNetworkSessionOptions();
            var small = CookingNetworkWireCodec.Encode(CookingNetworkMessageKind.Baseline, "frame-control", new { parserFixture = true });
            var wire = new byte[bounds.FrameBytes]; Array.Fill(wire, (byte)' '); small.CopyTo(wire, 0);
            Require(CookingNetworkWireCodec.TryDecode(wire, bounds, out _), "Exact wire limit Baseline-kind parser fixture (not typed business baseline)");
            var oversized = new byte[bounds.FrameBytes + 1]; Array.Fill(oversized, (byte)' '); small.CopyTo(oversized, 0);
            Require(!CookingNetworkWireCodec.TryDecode(oversized, bounds, out _), "Wire plus one rejected");
            passed.Add("wire-8MiB-exact-and-plus-one");
            Console.WriteLine(JsonSerializer.Serialize(new { passed = true, controls = passed, maximumBodyBytes = limit, wireBytes = bounds.FrameBytes, scope = "Application codec and wire guards; not actual transport/pair acceptance" }));
            return 0;
        } catch (Exception error) {
            Console.WriteLine(JsonSerializer.Serialize(new { passed = false, controls = passed, error = error.ToString() })); return 1;
        }
    }
    private static void RejectPrefix(IFrameDecoder decoder, int body)
    {
        var prefix = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(prefix, (uint)body); decoder.Append(new(prefix));
        try { decoder.TryRead(out _, out _); }
        catch (InvalidOperationException error) when (error.Message.StartsWith("Frame too large:", StringComparison.Ordinal)) { return; }
        throw new InvalidOperationException("Expected oversized prefix rejection.");
    }
    private static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
}
