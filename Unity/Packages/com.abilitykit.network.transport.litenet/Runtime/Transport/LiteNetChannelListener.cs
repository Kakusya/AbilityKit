using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using AbilityKit.Network.Host;
using LiteNetLib;

namespace AbilityKit.Network.Transport.LiteNet
{
    /// <summary>Reliable UDP admission. Transferred channels retain the shared manager after listener disposal.</summary>
    public sealed class LiteNetChannelListener : IChannelListener
    {
        private readonly object _gate = new object();
        private readonly IPAddress _address;
        private readonly int _port;
        private readonly string _key;
        private readonly int _maximumBufferedReceiveBytes;
        private readonly Dictionary<NetPeer, LiteNetServerChannel> _channels = new Dictionary<NetPeer, LiteNetServerChannel>();
        private NetManager _manager;
        private bool _listening;
        private bool _disposed;
        private long _nextId;

        public LiteNetChannelListener(IPAddress address = null, int port = 0, string connectionKey = "abilitykit",
            int maximumBufferedReceiveBytes = 8 * 1024 * 1024)
        {
            if (port < 0 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            if (maximumBufferedReceiveBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBufferedReceiveBytes));
            _address = address ?? IPAddress.Any; _port = port; _key = connectionKey ?? string.Empty;
            _maximumBufferedReceiveBytes = maximumBufferedReceiveBytes;
        }

        public bool IsListening { get { lock (_gate) return _listening; } }
        public string Endpoint { get; private set; } = string.Empty;
        public event Action<IServerChannel> ChannelAccepted;
        public event Action<Exception> Error;

        public void Start()
        {
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(LiteNetChannelListener));
                if (_listening) throw new InvalidOperationException("Listener already started.");
                if (_manager != null) { _listening = true; return; }
                var events = new EventBasedNetListener();
                events.ConnectionRequestEvent += request => { if (IsListening) request.AcceptIfKey(_key); else request.Reject(); };
                events.PeerConnectedEvent += OnConnected;
                events.PeerDisconnectedEvent += (peer, info) => {
                    LiteNetServerChannel channel;
                    lock (_gate) _channels.TryGetValue(peer, out channel);
                    channel?.Close();
                };
                events.NetworkReceiveEvent += (peer, reader, number, method) => {
                    LiteNetServerChannel channel;
                    lock (_gate) _channels.TryGetValue(peer, out channel);
                    if (channel != null) channel.Receive(reader.GetRemainingBytes());
                };
                events.NetworkErrorEvent += (endpoint, error) => Report(new System.Net.Sockets.SocketException((int)error));
                var manager = new NetManager(events) { UnsyncedEvents = true, AutoRecycle = true };
                try
                {
                    if (!manager.Start(_address, IPAddress.IPv6Any, _port)) throw new InvalidOperationException("UDP bind failed.");
                    _manager = manager;
                    Endpoint = _address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                        ? "[" + _address + "]:" + manager.LocalPort : _address + ":" + manager.LocalPort;
                    _listening = true;
                }
                catch { StopManager(manager); throw; }
            }
        }

        private void OnConnected(NetPeer peer)
        {
            LiteNetServerChannel channel;
            Action<IServerChannel> accepted;
            lock (_gate)
            {
                if (!_listening || _disposed) { channel = null; accepted = null; }
                else
                {
                    channel = new LiteNetServerChannel((++_nextId).ToString(System.Globalization.CultureInfo.InvariantCulture),
                        peer, _maximumBufferedReceiveBytes, () => Release(peer));
                    _channels.Add(peer, channel); accepted = ChannelAccepted;
                }
            }
            if (channel == null) { peer.Disconnect(); return; }
            // The acceptance callback is outside the manager gate. Its outstanding
            // channel lease also protects a concurrent listener Dispose.
            try { if (accepted == null) channel.Close(); else { accepted(channel); channel.Activate(); } }
            catch (Exception exception) { try { Report(exception); } finally { channel.Close(); } }
        }

        private void Release(NetPeer peer)
        {
            NetManager manager = null;
            lock (_gate)
            {
                _channels.Remove(peer);
                if (_disposed && _channels.Count == 0) { manager = _manager; _manager = null; }
            }
            if (manager != null) StopManager(manager);
        }

        private void Report(Exception exception)
        {
            if (Monitor.IsEntered(_gate)) { ThreadPool.QueueUserWorkItem(_ => Report(exception)); return; }
            var handlers = Error;
            if (handlers == null) return;
            foreach (Action<Exception> handler in handlers.GetInvocationList())
                try { handler(exception); } catch { /* User errors cannot escape the socket callback. */ }
        }

        // Stop can be requested by a receive/disconnect callback. Joining the
        // network thread from itself must never be part of releasing a lease.
        private static void StopManager(NetManager manager) => ThreadPool.QueueUserWorkItem(_ => { try { manager.Stop(); } catch { } });

        public void Stop() { lock (_gate) _listening = false; }
        public void Dispose()
        {
            NetManager manager = null;
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true; _listening = false;
                // Every mapped channel is either in its acceptance callback or
                // already transferred. Failed/no-subscriber acceptance releases it.
                if (_channels.Count == 0) { manager = _manager; _manager = null; }
            }
            if (manager != null) StopManager(manager);
        }
    }

    /// <summary>One owned reliable UDP peer with ordered, bounded receive delivery.</summary>
    public sealed class LiteNetServerChannel : IServerChannel
    {
        private readonly object _gate = new object();
        private readonly NetPeer _peer;
        private readonly Action _release;
        private readonly int _maximumBufferedReceiveBytes;
        private readonly Queue<byte[]> _pending = new Queue<byte[]>();
        private Action<ArraySegment<byte>> _bytesReceived;
        private int _bufferedBytes;
        private bool _dispatching;
        private bool _activated;
        private bool _closed;
        internal LiteNetServerChannel(string id, NetPeer peer, int maximumBufferedReceiveBytes, Action release)
        { Id = id; _peer = peer; _maximumBufferedReceiveBytes = maximumBufferedReceiveBytes; _release = release; }
        public string Id { get; }
        public string RemoteEndpoint => _peer.ToString();
        public bool IsConnected { get { lock (_gate) return !_closed && _peer.ConnectionState == LiteNetLib.ConnectionState.Connected; } }
        public event Action<ArraySegment<byte>> BytesReceived
        {
            add { lock (_gate) _bytesReceived += value; Drain(); }
            remove { lock (_gate) _bytesReceived -= value; }
        }
        public event Action<IServerChannel> Closed;
        public event Action<Exception> Error;

        internal void Activate() { lock (_gate) _activated = true; Drain(); }

        internal void Receive(byte[] bytes)
        {
            bool overflow;
            lock (_gate)
            {
                if (_closed || bytes.Length == 0) return;
                overflow = _pending.Count >= 1024 || bytes.Length > _maximumBufferedReceiveBytes - _bufferedBytes;
                if (!overflow) { _pending.Enqueue(bytes); _bufferedBytes += bytes.Length; }
            }
            if (overflow) { try { Report(new InvalidOperationException("Channel receive buffer limit exceeded.")); } finally { Close(); } }
            else Drain();
        }

        private void Drain()
        {
            lock (_gate) { if (_closed || !_activated || _dispatching || _bytesReceived == null) return; _dispatching = true; }
            while (true)
            {
                byte[] bytes; Action<ArraySegment<byte>> handlers;
                lock (_gate)
                {
                    if (_closed || _pending.Count == 0 || _bytesReceived == null) { _dispatching = false; return; }
                    bytes = _pending.Dequeue(); _bufferedBytes -= bytes.Length; handlers = _bytesReceived;
                }
                try { handlers(new ArraySegment<byte>(bytes)); }
                catch (Exception exception) { try { Report(exception); } finally { Close(); lock (_gate) _dispatching = false; } return; }
            }
        }

        private void Report(Exception exception)
        {
            if (Monitor.IsEntered(_gate)) { ThreadPool.QueueUserWorkItem(_ => Report(exception)); return; }
            var handlers = Error;
            if (handlers == null) return;
            foreach (Action<Exception> handler in handlers.GetInvocationList()) try { handler(exception); } catch { }
        }

        public void Send(ArraySegment<byte> bytes)
        {
            if (bytes.Array == null || bytes.Count == 0) return;
            try
            {
                lock (_gate)
                {
                    if (_closed || _peer.ConnectionState != LiteNetLib.ConnectionState.Connected) throw new InvalidOperationException("UDP channel disconnected.");
                    _peer.Send(bytes.Array, bytes.Offset, bytes.Count, DeliveryMethod.ReliableOrdered);
                }
            }
            catch (Exception exception) { try { Report(exception); } finally { Close(); } throw; }
        }
        public void Close()
        {
            lock (_gate) { if (_closed) return; _closed = true; _pending.Clear(); _bufferedBytes = 0; }
            try { _peer.Disconnect(); }
            catch (Exception exception) { Report(exception); }
            finally
            {
                try
                {
                    var handlers = Closed;
                    if (handlers != null) foreach (Action<IServerChannel> handler in handlers.GetInvocationList())
                        try { handler(this); } catch (Exception exception) { Report(exception); }
                }
                finally { _release(); }
            }
        }
        public void Dispose() => Close();
    }
}
