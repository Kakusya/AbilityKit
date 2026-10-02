using System.Text;
using System.Text.Json;
using AbilityKit.Game.Cooking.Session;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;
public sealed class CookingNetworkWireV3Tests
{
    private static readonly CookingLevelScope Scope = new(new(new("s"), new("w"), new("m")), new(1), new("l"), 1);
    [Fact]
    public void Instance_qualified_domain_ID_is_stable_across_transport_and_distinct_after_cold_restart()
    {
        var first = CookingNetworkWireCodec.DomainId("instance-a", Scope, new("p"), "stable");
        Assert.Equal(69, Encoding.UTF8.GetByteCount(first.Value));
        Assert.Equal(first, CookingNetworkWireCodec.DomainId("instance-a", Scope, new("p"), "stable"));
        Assert.NotEqual(first, CookingNetworkWireCodec.DomainId("instance-b", Scope, new("p"), "stable"));
        Assert.NotEqual(first, CookingNetworkWireCodec.DomainId("instance-a", Scope, new("q"), "stable"));
        Assert.NotEqual(CookingNetworkWireCodec.DomainId("ab", Scope, new("c"), "d"),
            CookingNetworkWireCodec.DomainId("a", Scope, new("bc"), "d"));
    }
    [Theory]
    [InlineData("{\"protocolVersion\":2,\"kind\":\"Join\",\"correlationId\":\"j\",\"payload\":{}}")]
    [InlineData("{\"kind\":\"Join\",\"correlationId\":\"j\",\"payload\":{}}")]
    [InlineData("{\"protocolVersion\":3,\"kind\":\"unknown\",\"correlationId\":\"j\",\"payload\":{}}")]
    [InlineData("{\"protocolVersion\":3,\"protocolVersion\":3,\"kind\":\"Join\",\"correlationId\":\"j\",\"payload\":{}}")]
    public void Envelope_rejects_old_missing_unknown_and_duplicate_fields(string json) =>
        Assert.False(CookingNetworkWireCodec.TryDecode(Encoding.UTF8.GetBytes(json), new(), out _));
    [Fact]
    public void Required_nullable_join_fields_are_present_null_not_omittable()
    {
        var bytes = CookingNetworkWireCodec.Encode(CookingNetworkMessageKind.Join, "j", new CookingNetworkJoin(new("p"), "credential", null, null));
        Assert.True(CookingNetworkWireCodec.TryDecode(bytes, new(), out var envelope));
        Assert.NotNull(CookingNetworkWireCodec.Read<CookingNetworkJoin>(envelope!));
        var missing = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace(",\"rebindToken\":null", ""));
        Assert.True(CookingNetworkWireCodec.TryDecode(missing, new(), out envelope));
        Assert.Null(CookingNetworkWireCodec.Read<CookingNetworkJoin>(envelope!));
    }
    [Fact]
    public void Streaming_bounds_reject_oversized_collection_frame_string_and_depth()
    {
        byte[] Envelope(string payload) => Encoding.UTF8.GetBytes("{\"protocolVersion\":3,\"kind\":\"Baseline\",\"correlationId\":\"b\",\"payload\":" + payload + "}");
        Assert.False(CookingNetworkWireCodec.TryDecode(Envelope("[" + string.Join(',', Enumerable.Repeat("0", 16385)) + "]"), new(), out _));
        Assert.False(CookingNetworkWireCodec.TryDecode(Envelope("\"" + new string('x', 1025) + "\""), new(), out _));
        Assert.False(CookingNetworkWireCodec.TryDecode(Envelope(new string('[', 33) + "0" + new string(']', 33)), new(), out _));
        Assert.False(CookingNetworkWireCodec.TryDecode(Envelope("{}"), new(FrameBytes: 8), out _));
    }
    [Fact]
    public void Baseline_collection_expansion_does_not_expand_command_or_control_bounds()
    {
        var payload = "[" + string.Join(',', Enumerable.Repeat("0", 4097)) + "]";
        byte[] Envelope(string kind) => Encoding.UTF8.GetBytes("{\"protocolVersion\":3,\"kind\":\"" + kind + "\",\"correlationId\":\"b\",\"payload\":" + payload + "}");
        Assert.True(CookingNetworkWireCodec.TryDecode(Envelope("Baseline"), new(), out _));
        Assert.False(CookingNetworkWireCodec.TryDecode(Envelope("Command"), new(), out _));
        Assert.False(CookingNetworkWireCodec.TryDecode(Envelope("Join"), new(), out _));
    }

    private static CookingNetworkWireEnvelope TypedEnvelope<T>(T payload)
    {
        var bytes = CookingNetworkWireCodec.Encode(CookingNetworkMessageKind.Baseline, "typed-read", payload);
        Assert.True(CookingNetworkWireCodec.TryDecode(bytes, new(), out var envelope)); return envelope!;
    }
    [Fact]
    public void Cached_type_metadata_still_rejects_later_instance_null_fields_and_nested_collection_nulls()
    {
        var valid = new CookingNetworkJoin(new("p"), "credential", null, null);
        Assert.NotNull(CookingNetworkWireCodec.Read<CookingNetworkJoin>(TypedEnvelope(valid)));
        Assert.Null(CookingNetworkWireCodec.Read<CookingNetworkJoin>(TypedEnvelope(valid with { JoinCredential = null! })));
        Assert.Null(CookingNetworkWireCodec.Read<CookingNetworkJoin>(TypedEnvelope(valid with { Participant = new(null!) })));
        Assert.NotNull(CookingNetworkWireCodec.Read<CookingNetworkJoin>(TypedEnvelope(valid))); // Present nullable fields remain legal after invalid instances.
        var participant = new CookingNetworkParticipantProjection(new("p"), true, 1, 0, 0, false, false);
        var projection = new CookingNetworkSessionProjection("instance", Array.AsReadOnly(new[] { participant }));
        Assert.NotNull(CookingNetworkWireCodec.Read<CookingNetworkSessionProjection>(TypedEnvelope(projection)));
        Assert.Null(CookingNetworkWireCodec.Read<CookingNetworkSessionProjection>(TypedEnvelope(projection with {
            Participants = Array.AsReadOnly(new[] { participant with { Participant = new(null!) } }) })));
        Assert.Null(CookingNetworkWireCodec.Read<CookingNetworkSessionProjection>(TypedEnvelope(projection with {
            Participants = Array.AsReadOnly(new CookingNetworkParticipantProjection[] { null! }) })));
        Assert.NotNull(CookingNetworkWireCodec.Read<CookingNetworkSessionProjection>(TypedEnvelope(projection)));
    }
    [Fact]
    public void Concurrent_typed_reads_share_only_metadata_and_validate_every_instance()
    {
        var participant = new CookingNetworkParticipantProjection(new("parallel"), true, 1, 0, 0, false, false);
        var valid = TypedEnvelope(new CookingNetworkSessionProjection("instance", Array.AsReadOnly(new[] { participant })));
        var bad = TypedEnvelope(new CookingNetworkSessionProjection("instance", Array.AsReadOnly(new[] {
            participant with { Participant = new(null!) } })));
        Parallel.For(0, 128, i => {
            Assert.NotNull(CookingNetworkWireCodec.Read<CookingNetworkSessionProjection>(valid));
            Assert.Null(CookingNetworkWireCodec.Read<CookingNetworkSessionProjection>(bad));
        });
    }

}
