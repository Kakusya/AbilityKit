using System.Net;
using AbilityKit.Game.Cooking.Session;
using AbilityKit.Network.Host;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Transport.LiteNet;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

// Actual UDP messages, not a substituted transport or synthetic authority fixture.
public sealed class CookingLiteNetFramingBoundaryTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(8);
    private const string Key = "cooking-framing-boundary";
    private static TaskCompletionSource<T> Completion<T>() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static byte[] Wire(int size)
    {
        var small = CookingNetworkWireCodec.Encode(CookingNetworkMessageKind.Baseline, "boundary", new { parserFixture = true });
        var bytes = new byte[size]; Array.Fill(bytes, (byte)' '); small.CopyTo(bytes, 0); return bytes;
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Actual_udp_malformed_prefix_or_body_is_rejected_by_bounded_host_decoder(bool oversizedPrefix)
    {
        var bounds = new CookingNetworkSessionOptions();
        using var listener = new LiteNetChannelListener(IPAddress.Loopback, connectionKey: Key,
            maximumBufferedReceiveBytes: bounds.FrameBytes + 20);
        var requests = 0; var rejected = Completion<Exception>();
        var router = new ServerRequestRouter().Register(CookingNetworkWireCodec.OpCode, (_, _, _) => Interlocked.Increment(ref requests));
        using var host = new NetworkHost(listener, new NetworkHostOptions { FrameCodec = new BoundedCodec(bounds.FrameBytes + 64), RequestHandler = router });
        host.SessionError += (_, error) => rejected.TrySetResult(error); host.Start();
        using var client = new LiteNetTransport(Key); var connected = Completion<bool>(); client.Connected += () => connected.TrySetResult(true);
        client.Connect("127.0.0.1", int.Parse(listener.Endpoint[(listener.Endpoint.LastIndexOf(':') + 1)..])); await connected.Task.WaitAsync(Timeout);
        byte[] frame;
        if (oversizedPrefix) { frame = new byte[4]; System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(frame, (uint)(bounds.FrameBytes + 65)); }
        else {
            // Advertised body20 contains header16 plus payload4, but header declares payload5.
            frame = new byte[24]; System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(frame, 20);
            new NetworkPacketHeader(default, CookingNetworkWireCodec.OpCode, 1, 5).Write(frame.AsSpan(4, 16));
        }
        client.Send(new(frame)); var error = await rejected.Task.WaitAsync(Timeout);
        Assert.Contains(oversizedPrefix ? "Frame too large" : "Invalid frame", error.Message); Assert.Equal(0, Volatile.Read(ref requests));
    }
    private sealed class BoundedCodec(int maximum) : AbilityKit.Network.Abstractions.IFrameCodec
    {
        public AbilityKit.Network.Abstractions.IFrameDecoder CreateDecoder() => new Decoder(maximum);
        public ArraySegment<byte> Encode(NetworkPacketHeader header, ArraySegment<byte> payload) => LengthPrefixedFrameCodec.Instance.Encode(header, payload);
        private sealed class Decoder(int maximum) : AbilityKit.Network.Abstractions.IFrameDecoder
        {
            private readonly NetworkFrameReader _reader = new() { MaxFrameLength = maximum };
            public void Reset() => _reader.Reset(); public void Append(ArraySegment<byte> bytes) => _reader.Append(bytes);
            public bool TryRead(out NetworkPacketHeader header, out ArraySegment<byte> payload) => _reader.TryRead(out header, out payload);
        }
    }
    [Fact]
    public async Task Exact_wire_limit_plus_twenty_framing_bytes_traverses_actual_udp_both_directions()
    {
        var bounds = new CookingNetworkSessionOptions(); var wire = Wire(bounds.FrameBytes);
        Assert.True(CookingNetworkWireCodec.TryDecode(wire, bounds, out _));
        using var listener = new LiteNetChannelListener(IPAddress.Loopback, connectionKey: Key,
            maximumBufferedReceiveBytes: checked(bounds.FrameBytes + 4 + NetworkPacketHeader.Size));
        var accepted = Completion<IServerChannel>(); listener.ChannelAccepted += c => accepted.TrySetResult(c); listener.Start();
        using var client = new LiteNetTransport(Key); var connected = Completion<bool>(); client.Connected += () => connected.TrySetResult(true);
        var received = Completion<byte[]>(); client.BytesReceived += b => received.TrySetResult(b.ToArray());
        client.Connect("127.0.0.1", int.Parse(listener.Endpoint[(listener.Endpoint.LastIndexOf(':') + 1)..]));
        await connected.Task.WaitAsync(Timeout); using var channel = await accepted.Task.WaitAsync(Timeout);
        var incoming = Completion<byte[]>(); channel.BytesReceived += b => { incoming.TrySetResult(b.ToArray()); channel.Send(b); };
        var frame = LengthPrefixedFrameCodec.Instance.Encode(new(default, CookingNetworkWireCodec.OpCode, 1, (uint)wire.Length), new(wire));
        Assert.Equal(bounds.FrameBytes + 20, frame.Count); client.Send(frame);
        var serverBytes = await incoming.Task.WaitAsync(Timeout); var clientBytes = await received.Task.WaitAsync(Timeout);
        Assert.True(frame.AsSpan().SequenceEqual(serverBytes)); Assert.True(serverBytes.AsSpan().SequenceEqual(clientBytes));
        var reader = new NetworkFrameReader { MaxFrameLength = bounds.FrameBytes + 64 }; reader.Append(new(clientBytes));
        Assert.True(reader.TryRead(out var header, out var payload)); Assert.Equal(CookingNetworkWireCodec.OpCode, header.OpCode);
        Assert.True(payload.AsSpan().SequenceEqual(wire)); Assert.True(CookingNetworkWireCodec.TryDecode(payload.AsSpan(), bounds, out _));
        Assert.False(reader.TryRead(out _, out _));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Actual_udp_rejects_storage_plus_one_and_preserves_generic_default(bool applicationAllowance)
    {
        var bounds = new CookingNetworkSessionOptions();
        var capacity = bounds.FrameBytes + (applicationAllowance ? 20 : 0);
        using var listener = applicationAllowance
            ? new LiteNetChannelListener(IPAddress.Loopback, connectionKey: Key, maximumBufferedReceiveBytes: capacity)
            : new LiteNetChannelListener(IPAddress.Loopback, connectionKey: Key);
        var accepted = Completion<IServerChannel>(); listener.ChannelAccepted += c => accepted.TrySetResult(c); listener.Start();
        using var client = new LiteNetTransport(Key); var connected = Completion<bool>(); client.Connected += () => connected.TrySetResult(true);
        client.Connect("127.0.0.1", int.Parse(listener.Endpoint[(listener.Endpoint.LastIndexOf(':') + 1)..]));
        await connected.Task.WaitAsync(Timeout); using var channel = await accepted.Task.WaitAsync(Timeout);
        var error = Completion<Exception>(); var closed = Completion<bool>(); var delivered = 0;
        channel.Error += e => error.TrySetResult(e); channel.Closed += _ => closed.TrySetResult(true);
        channel.BytesReceived += _ => Interlocked.Increment(ref delivered);
        var wire = Wire(bounds.FrameBytes + (applicationAllowance ? 1 : 0));
        Assert.Equal(!applicationAllowance, CookingNetworkWireCodec.TryDecode(wire, bounds, out _));
        var frame = LengthPrefixedFrameCodec.Instance.Encode(new(default, CookingNetworkWireCodec.OpCode, 1, (uint)wire.Length), new(wire));
        Assert.True(frame.Count > capacity); client.Send(frame);
        Assert.Contains("Channel receive buffer limit exceeded", (await error.Task.WaitAsync(Timeout)).Message);
        await closed.Task.WaitAsync(Timeout); Assert.Equal(0, Volatile.Read(ref delivered)); Assert.False(channel.IsConnected);
    }
}
