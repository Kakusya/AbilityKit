using System;
using System.Threading;
using AbilityKit.Network.Abstractions;
using LiteNetLib;

namespace AbilityKit.Network.Transport.LiteNet
{
    /// <summary>Owned reliable-ordered UDP client. Close permits reconnect; Dispose is terminal.</summary>
    public sealed class LiteNetTransport : ITransport
    {
        private readonly object _gate = new object();
        private readonly string _connectionKey;
        private NetManager _manager;
        private NetPeer _peer;
        private bool _disposed;
        public LiteNetTransport(string connectionKey = "abilitykit") { _connectionKey = connectionKey ?? string.Empty; }
        public bool IsConnected { get { lock (_gate) return !_disposed && _peer != null && _peer.ConnectionState == LiteNetLib.ConnectionState.Connected; } }
        public event Action Connected;
        public event Action Disconnected;
        public event Action<Exception> Error;
        public event Action<ArraySegment<byte>> BytesReceived;

        public void Connect(string host, int port)
        {
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Host is required.", nameof(host));
            if (port <= 0 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            NetManager manager;
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(LiteNetTransport));
                if (_manager != null) throw new InvalidOperationException("Transport already started.");
                var listener = new EventBasedNetListener();
                manager = new NetManager(listener) { UnsyncedEvents = true, AutoRecycle = true };
                listener.PeerConnectedEvent += peer => {
                    lock (_gate) { if (!ReferenceEquals(_manager, manager)) return; _peer = peer; }
                    try { Connected?.Invoke(); } catch (Exception exception) { try { Report(exception); } finally { CloseCurrent(manager); } }
                };
                listener.PeerDisconnectedEvent += (peer, info) => {
                    lock (_gate) { if (!ReferenceEquals(_manager, manager)) return; _peer = null; }
                    // Rejected/failed connects also terminate the attempt even
                    // when PeerConnected never populated the handoff peer.
                    NotifyDisconnected();
                };
                listener.NetworkReceiveEvent += (peer, reader, channel, method) => {
                    lock (_gate) { if (!ReferenceEquals(_manager, manager)) return; }
                    var bytes = reader.GetRemainingBytes();
                    if (bytes.Length == 0) return;
                    try { BytesReceived?.Invoke(new ArraySegment<byte>(bytes)); }
                    catch (Exception exception) { try { Report(exception); } finally { CloseCurrent(manager); } }
                };
                listener.NetworkErrorEvent += (endpoint, error) => {
                    lock (_gate) { if (!ReferenceEquals(_manager, manager)) return; }
                    try { Report(new System.Net.Sockets.SocketException((int)error)); } finally { CloseCurrent(manager); }
                };
                _manager = manager;
            }
            try
            {
                lock (_gate)
                {
                    if (_disposed || !ReferenceEquals(_manager, manager)) throw new ObjectDisposedException(nameof(LiteNetTransport));
                    if (!manager.Start()) throw new InvalidOperationException("UDP client bind failed.");
                    manager.Connect(host, port, _connectionKey);
                }
            }
            catch (Exception exception) { try { Report(exception); } finally { CloseCurrent(manager); } throw; }
        }

        public void Send(ArraySegment<byte> bytes)
        {
            if (bytes.Array == null || bytes.Count == 0) return;
            NetManager manager;
            lock (_gate) manager = _manager;
            try
            {
                lock (_gate)
                {
                    if (_disposed) throw new ObjectDisposedException(nameof(LiteNetTransport));
                    if (_peer == null || _peer.ConnectionState != LiteNetLib.ConnectionState.Connected)
                        throw new InvalidOperationException("Not connected.");
                    _peer.Send(bytes.Array, bytes.Offset, bytes.Count, DeliveryMethod.ReliableOrdered);
                }
            }
            catch (Exception exception) { try { Report(exception); } finally { CloseCurrent(manager); } throw; }
        }
        private void Report(Exception exception)
        {
            if (Monitor.IsEntered(_gate)) { ThreadPool.QueueUserWorkItem(_ => Report(exception)); return; }
            var handlers = Error;
            if (handlers != null) foreach (Action<Exception> handler in handlers.GetInvocationList()) try { handler(exception); } catch { }
        }
        private void NotifyDisconnected()
        {
            var handlers = Disconnected;
            if (handlers != null) foreach (Action handler in handlers.GetInvocationList()) try { handler(); } catch (Exception exception) { Report(exception); }
        }
        private void CloseCurrent(NetManager expected)
        {
            NetManager manager; bool notify;
            lock (_gate)
            {
                if (!ReferenceEquals(_manager, expected)) return;
                manager = _manager; _manager = null; notify = _peer != null; _peer = null;
            }
            if (manager != null) ThreadPool.QueueUserWorkItem(_ => { try { manager.Stop(); } catch { } });
            if (notify) NotifyDisconnected();
        }
        public void Close() { NetManager manager; lock (_gate) manager = _manager; CloseCurrent(manager); }
        public void Dispose() { lock (_gate) _disposed = true; Close(); }
    }
}
