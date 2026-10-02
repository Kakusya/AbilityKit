using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Host;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using LiteNetLib;
using Xunit;

namespace AbilityKit.Network.Transport.LiteNet.Tests;

public sealed class LiteNetChannelListenerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(8);
    private const string Key = "generic-litenet-tests";
    private static TaskCompletionSource<T> Completion<T>() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static int Port(LiteNetChannelListener listener) => int.Parse(listener.Endpoint[(listener.Endpoint.LastIndexOf(':') + 1)..]);

    [Fact]
    public async Task Two_peers_use_existing_host_framing_pipeline_and_exact_payload_slices()
    {
        var listener = new LiteNetChannelListener(IPAddress.Loopback, connectionKey: Key);
        var middleware = new ProbeMiddleware();
        var router = new ServerRequestRouter().Register(42, (session, header, payload) => session.SendResponse(header.OpCode, header.Seq, payload));
        using var host = new NetworkHost(listener, new NetworkHostOptions { RequestHandler = router, ConfigurePipeline = b => b.Use(middleware) });
        host.Start();
        using var first = await OpenClient(Port(listener)); using var second = await OpenClient(Port(listener));
        var responses = await Task.WhenAll(Request(first, 101, new byte[] { 9, 1, 2, 9 }), Request(second, 202, new byte[] { 8, 3, 4, 8 }));
        Assert.Equal(new byte[] { 1, 2 }, responses[0]); Assert.Equal(new byte[] { 3, 4 }, responses[1]);
        Assert.Equal(2, host.SessionCount); Assert.Equal(2, middleware.Inbound); Assert.Equal(2, middleware.Outbound);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Admission_stop_and_listener_dispose_preserve_transferred_peer_until_owner_closes(bool dispose)
    {
        using var pair = await Pair.Open();
        pair.Channel.BytesReceived += bytes => pair.Channel.Send(bytes);
        pair.Client.Send(new(new byte[] { 1 })); Assert.Equal(new byte[] { 1 }, await pair.Read());
        if (dispose) pair.Listener.Dispose(); else pair.Listener.Stop();
        Assert.False(pair.Listener.IsListening);
        pair.Client.Send(new(new byte[] { 2 })); Assert.Equal(new byte[] { 2 }, await pair.Read());
        if (!dispose)
        {
            pair.Listener.Start(); Assert.True(pair.Listener.IsListening);
            var next = Completion<IServerChannel>(); pair.Listener.ChannelAccepted += c => next.TrySetResult(c);
            using var other = await OpenClient(Port(pair.Listener)); using var otherChannel = await next.Task.WaitAsync(Timeout);
            Assert.NotEqual(pair.Channel.Id, otherChannel.Id);
        }
        var closed = 0; pair.Channel.Closed += _ => Interlocked.Increment(ref closed);
        pair.Channel.Dispose(); pair.Channel.Close(); Assert.Equal(1, closed);
        Assert.False(pair.Channel.IsConnected);
    }

    [Fact]
    public async Task Frames_received_before_handler_installation_are_owned_and_delivered_in_order()
    {
        using var listener = new LiteNetChannelListener(IPAddress.Loopback, connectionKey: Key);
        var accepted = Completion<IServerChannel>(); listener.ChannelAccepted += c => accepted.TrySetResult(c); listener.Start();
        var events = new EventBasedNetListener();
        var connected = Completion<NetPeer>(); events.PeerConnectedEvent += p => connected.TrySetResult(p);
        var delivered = Completion<bool>(); events.DeliveryEvent += (p, marker) => { if ((string)marker == "second") delivered.TrySetResult(true); };
        var manager = new NetManager(events) { UnsyncedEvents = true, UnsyncedDeliveryEvent = true, AutoRecycle = true };
        try
        {
            Assert.True(manager.Start()); manager.Connect("127.0.0.1", Port(listener), Key);
            var peer = await connected.Task.WaitAsync(Timeout); using var channel = await accepted.Task.WaitAsync(Timeout);
            peer.SendWithDeliveryEvent(new byte[] { 1, 2 }, 0, DeliveryMethod.ReliableOrdered, "first");
            peer.SendWithDeliveryEvent(new byte[] { 3, 4 }, 0, DeliveryMethod.ReliableOrdered, "second");
            await delivered.Task.WaitAsync(Timeout);
            var payloads = new ConcurrentQueue<ArraySegment<byte>>(); var received = Completion<bool>();
            channel.BytesReceived += bytes => { payloads.Enqueue(bytes); if (payloads.Count == 2) received.TrySetResult(true); };
            await received.Task.WaitAsync(Timeout);
            Assert.Equal(new byte[] { 1, 2 }, payloads.ElementAt(0).ToArray());
            Assert.Equal(new byte[] { 3, 4 }, payloads.ElementAt(1).ToArray());
            Assert.NotSame(payloads.ElementAt(0).Array, payloads.ElementAt(1).Array);
        }
        finally { manager.Stop(); }
    }

    [Fact]
    public async Task Throwing_receive_error_and_closed_subscribers_cannot_prevent_cleanup_or_later_subscribers()
    {
        using var pair = await Pair.Open();
        var closed = Completion<bool>(); var errors = 0;
        pair.Channel.Error += _ => throw new InvalidOperationException("error subscriber");
        pair.Channel.Error += _ => Interlocked.Increment(ref errors);
        pair.Channel.Closed += _ => throw new InvalidOperationException("closed subscriber");
        pair.Channel.Closed += _ => closed.TrySetResult(true);
        pair.Channel.BytesReceived += _ => throw new InvalidOperationException("receive subscriber");
        pair.Client.Send(new(new byte[] { 1 })); await closed.Task.WaitAsync(Timeout);
        Assert.False(pair.Channel.IsConnected); Assert.True(errors >= 1);
        pair.Channel.Close(); Assert.True(closed.Task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Concurrent_send_close_dispose_notifies_once_and_does_not_block_other_peer()
    {
        using var pair = await Pair.Open();
        var secondAccepted = Completion<IServerChannel>(); pair.Listener.ChannelAccepted += c => secondAccepted.TrySetResult(c);
        using var other = await OpenClient(Port(pair.Listener)); using var otherChannel = await secondAccepted.Task.WaitAsync(Timeout);
        var echoed = Completion<byte[]>(); other.BytesReceived += b => echoed.TrySetResult(b.ToArray());
        otherChannel.BytesReceived += b => otherChannel.Send(b);
        var closed = 0; pair.Channel.Closed += _ => Interlocked.Increment(ref closed);
        await Task.WhenAll(Enumerable.Range(0, 24).Select(i => Task.Run(() => {
            if (i % 3 == 0) pair.Channel.Close();
            else if (i % 3 == 1) pair.Channel.Dispose();
            else try { pair.Channel.Send(new(new byte[] { 7 })); } catch (InvalidOperationException) { }
        }))).WaitAsync(Timeout);
        Assert.Equal(1, closed);
        other.Send(new(new byte[] { 6 })); Assert.Equal(new byte[] { 6 }, await echoed.Task.WaitAsync(Timeout));
    }

    [Fact]
    public async Task Receive_buffer_overflow_closes_unhandled_peer_instead_of_growing_without_bound()
    {
        using var pair = await Pair.Open(maximumBufferedReceiveBytes: 2);
        var closed = Completion<bool>(); pair.Channel.Closed += _ => closed.TrySetResult(true);
        pair.Client.Send(new(new byte[] { 1, 2, 3 })); await closed.Task.WaitAsync(Timeout);
        Assert.False(pair.Channel.IsConnected);
    }

    [Fact]
    public void Failed_real_bind_is_clean_and_listener_can_start_after_port_is_released()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) { ExclusiveAddressUse = true };
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0)); var port = ((IPEndPoint)socket.LocalEndPoint!).Port;
        using var listener = new LiteNetChannelListener(IPAddress.Loopback, port, Key);
        Assert.Throws<InvalidOperationException>(() => listener.Start()); Assert.False(listener.IsListening);
        socket.Dispose(); listener.Start(); Assert.True(listener.IsListening);
        Assert.Throws<InvalidOperationException>(() => listener.Start());
        listener.Dispose(); Assert.Throws<ObjectDisposedException>(() => listener.Start());
    }

    [Fact]
    public async Task Throwing_accept_and_listener_error_subscribers_release_rejected_peer_and_allow_next_admission()
    {
        using var listener = new LiteNetChannelListener(IPAddress.Loopback, connectionKey: Key);
        var error = Completion<bool>();
        Action<IServerChannel> fail = _ => throw new InvalidOperationException("accept subscriber");
        listener.ChannelAccepted += fail; listener.Error += _ => throw new InvalidOperationException("listener error");
        listener.Error += _ => error.TrySetResult(true); listener.Start();
        using var first = await OpenClient(Port(listener)); await error.Task.WaitAsync(Timeout);
        listener.ChannelAccepted -= fail;
        var accepted = Completion<IServerChannel>(); listener.ChannelAccepted += c => accepted.TrySetResult(c);
        using var second = await OpenClient(Port(listener)); using var channel = await accepted.Task.WaitAsync(Timeout);
        Assert.True(channel.IsConnected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Existing_host_async_drain_and_disconnect_cancellation_work_over_real_udp(bool disconnect)
    {
        var handler = new WaitingHandler();
        using var listener = new LiteNetChannelListener(IPAddress.Loopback, connectionKey: Key);
        using var host = new NetworkHost(listener, new NetworkHostOptions { AsyncRequestHandler = handler }); host.Start();
        using var client = await OpenClient(Port(listener));
        var disconnected = Completion<bool>(); client.Disconnected += () => disconnected.TrySetResult(true);
        client.Send(Frame(5, new(new byte[] { 1 }))); await handler.Started.Task.WaitAsync(Timeout);
        if (disconnect) { client.Close(); await handler.Cancelled.Task.WaitAsync(Timeout); }
        else
        {
            var stopping = host.StopAsync(Timeout); handler.Release.TrySetResult(true);
            await stopping.WaitAsync(Timeout); Assert.True(handler.Completed.Task.IsCompletedSuccessfully);
            Assert.Equal(1, host.GetDiagnostics().GracefulStops);
            await disconnected.Task.WaitAsync(Timeout);
        }
        Assert.False(client.IsConnected);
    }

    [Fact]
    public async Task Client_close_can_reconnect_and_dispose_is_terminal_even_with_throwing_disconnect_subscriber()
    {
        using var pair = await Pair.Open();
        pair.Client.Disconnected += () => throw new InvalidOperationException("disconnect subscriber");
        pair.Client.Close(); Assert.False(pair.Client.IsConnected);
        var connected = Completion<bool>(); pair.Client.Connected += () => connected.TrySetResult(true);
        var accepted = Completion<IServerChannel>(); pair.Listener.ChannelAccepted += c => accepted.TrySetResult(c);
        pair.Client.Connect("127.0.0.1", Port(pair.Listener)); await connected.Task.WaitAsync(Timeout);
        using var channel = await accepted.Task.WaitAsync(Timeout); Assert.True(pair.Client.IsConnected);
        pair.Client.Dispose(); Assert.False(pair.Client.IsConnected);
        Assert.Throws<ObjectDisposedException>(() => pair.Client.Connect("127.0.0.1", Port(pair.Listener)));
    }

    [Fact]
    public async Task Deferred_serial_io_dispatch_preserves_copied_payloads_after_later_receive()
    {
        var dispatcher = new DeferredDispatcher(); var received = new ConcurrentQueue<byte[]>(); var done = Completion<bool>();
        var router = new ServerRequestRouter().Register(42, (_, _, payload) => { received.Enqueue(payload.ToArray()); if (received.Count == 2) done.TrySetResult(true); });
        using var listener = new LiteNetChannelListener(IPAddress.Loopback, connectionKey: Key);
        using var host = new NetworkHost(listener, new NetworkHostOptions { RequestHandler = router, IoDispatcher = dispatcher }); host.Start();
        using var client = await OpenClient(Port(listener));
        client.Send(Frame(1, new(new byte[] { 1, 2 }))); await dispatcher.Posted.WaitAsync(Timeout);
        client.Send(Frame(2, new(new byte[] { 3, 4 }))); await dispatcher.Posted.WaitAsync(Timeout);
        Assert.Empty(received); dispatcher.Drain(); await done.Task.WaitAsync(Timeout);
        Assert.Equal(new byte[] { 1, 2 }, received.ElementAt(0)); Assert.Equal(new byte[] { 3, 4 }, received.ElementAt(1));
    }

    [Fact]
    public async Task Existing_frame_limit_reports_error_without_delivering_business_request()
    {
        using var listener = new LiteNetChannelListener(IPAddress.Loopback, connectionKey: Key);
        var requests = 0; var rejected = Completion<Exception>();
        var router = new ServerRequestRouter().Register(42, (_, _, _) => Interlocked.Increment(ref requests));
        using var host = new NetworkHost(listener, new NetworkHostOptions { RequestHandler = router });
        host.SessionError += (_, exception) => rejected.TrySetResult(exception); host.Start();
        using var client = await OpenClient(Port(listener));
        var prefix = new byte[4]; System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(prefix, 4 * 1024 * 1024 + 1);
        client.Send(new(prefix)); var exception = await rejected.Task.WaitAsync(Timeout);
        Assert.Contains("Frame too large", exception.Message); Assert.Equal(0, requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Wrong_key_or_stopped_admission_rejects_peer_without_channel_transfer(bool stopped)
    {
        using var listener = new LiteNetChannelListener(IPAddress.Loopback, connectionKey: Key);
        var accepted = 0; listener.ChannelAccepted += c => { Interlocked.Increment(ref accepted); c.Dispose(); }; listener.Start();
        if (stopped) listener.Stop();
        var events = new EventBasedNetListener(); var rejected = Completion<bool>(); events.PeerDisconnectedEvent += (_, _) => rejected.TrySetResult(true);
        var manager = new NetManager(events) { UnsyncedEvents = true, AutoRecycle = true };
        try
        {
            Assert.True(manager.Start()); manager.Connect("127.0.0.1", Port(listener), stopped ? Key : "wrong-key");
            await rejected.Task.WaitAsync(Timeout); Assert.Equal(0, accepted);
        }
        finally { manager.Stop(); }
    }

    [Fact]
    public async Task Client_reports_rejected_connection_attempt_before_any_connected_peer()
    {
        using var listener = new LiteNetChannelListener(IPAddress.Loopback, connectionKey: Key); listener.Start();
        using var client = new LiteNetTransport("wrong-key"); var rejected = Completion<bool>();
        client.Disconnected += () => rejected.TrySetResult(true);
        client.Connect("127.0.0.1", Port(listener)); await rejected.Task.WaitAsync(Timeout); Assert.False(client.IsConnected);
    }

    private static ArraySegment<byte> Frame(uint sequence, ArraySegment<byte> payload) => LengthPrefixedFrameCodec.Instance.Encode(new(NetworkPacketFlags.Request, 42, sequence, (uint)payload.Count), payload);
    private static async Task<byte[]> Request(LiteNetTransport client, uint sequence, byte[] storage)
    {
        var done = Completion<byte[]>(); var decoder = LengthPrefixedFrameCodec.Instance.CreateDecoder();
        Action<ArraySegment<byte>> receive = bytes => {
            decoder.Append(bytes);
            while (decoder.TryRead(out var header, out var payload))
            {
                Assert.Equal((uint)42, header.OpCode); Assert.Equal(sequence, header.Seq);
                Assert.True((header.Flags & NetworkPacketFlags.Response) != 0); done.TrySetResult(payload.ToArray());
            }
        };
        client.BytesReceived += receive;
        try { client.Send(Frame(sequence, new(storage, 1, 2))); return await done.Task.WaitAsync(Timeout); }
        finally { client.BytesReceived -= receive; }
    }
    private static async Task<LiteNetTransport> OpenClient(int port)
    {
        var client = new LiteNetTransport(Key); var connected = Completion<bool>(); client.Connected += () => connected.TrySetResult(true);
        try { client.Connect("127.0.0.1", port); await connected.Task.WaitAsync(Timeout); return client; }
        catch { client.Dispose(); throw; }
    }
    private sealed class Pair : IDisposable
    {
        public LiteNetChannelListener Listener { get; }
        public LiteNetTransport Client { get; }
        public IServerChannel Channel { get; }
        private readonly ConcurrentQueue<byte[]> _received = new(); private readonly SemaphoreSlim _signal = new(0);
        private Pair(LiteNetChannelListener listener, LiteNetTransport client, IServerChannel channel)
        { Listener = listener; Client = client; Channel = channel; client.BytesReceived += b => { _received.Enqueue(b.ToArray()); _signal.Release(); }; }
        public static async Task<Pair> Open(int maximumBufferedReceiveBytes = 8 * 1024 * 1024)
        {
            var listener = new LiteNetChannelListener(IPAddress.Loopback, connectionKey: Key, maximumBufferedReceiveBytes: maximumBufferedReceiveBytes);
            var accepted = Completion<IServerChannel>(); listener.ChannelAccepted += c => accepted.TrySetResult(c); listener.Start();
            LiteNetTransport? client = null;
            try { client = await OpenClient(Port(listener)); return new(listener, client, await accepted.Task.WaitAsync(Timeout)); }
            catch { client?.Dispose(); listener.Dispose(); throw; }
        }
        public async Task<byte[]> Read() { Assert.True(await _signal.WaitAsync(Timeout)); Assert.True(_received.TryDequeue(out var bytes)); return bytes!; }
        public void Dispose() { Channel.Dispose(); Client.Dispose(); Listener.Dispose(); }
    }
    private sealed class ProbeMiddleware : INetworkMiddleware
    {
        public int Inbound, Outbound;
        public void OnInbound(ISessionContext context, NetworkPacketHeader header, ArraySegment<byte> payload, Action<NetworkPacketHeader, ArraySegment<byte>> next)
        { Interlocked.Increment(ref Inbound); next(header, payload); }
        public void OnOutbound(ISessionContext context, NetworkPacketHeader header, ArraySegment<byte> payload, Action<NetworkPacketHeader, ArraySegment<byte>> next)
        { Interlocked.Increment(ref Outbound); next(header, payload); }
    }
    private sealed class WaitingHandler : IAsyncServerRequestHandler
    {
        public readonly TaskCompletionSource<bool> Started = Completion<bool>(), Release = Completion<bool>(), Cancelled = Completion<bool>(), Completed = Completion<bool>();
        public async Task HandleAsync(IServerNetworkSession session, NetworkPacketHeader header, ArraySegment<byte> payload, CancellationToken cancellationToken)
        {
            Started.TrySetResult(true);
            try { await Release.Task.WaitAsync(cancellationToken); Completed.TrySetResult(true); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { Cancelled.TrySetResult(true); throw; }
        }
    }
    private sealed class DeferredDispatcher : IDispatcher
    {
        private readonly ConcurrentQueue<Action> _actions = new();
        public readonly SemaphoreSlim Posted = new(0);
        public void Post(Action action) { _actions.Enqueue(action); Posted.Release(); }
        public void Drain() { while (_actions.TryDequeue(out var action)) action(); }
    }
}
