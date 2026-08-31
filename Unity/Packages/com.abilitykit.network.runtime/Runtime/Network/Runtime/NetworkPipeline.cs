using System;
using System.Collections.Generic;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Network.Runtime
{
    public sealed class NetworkPipeline
    {
        private readonly List<INetworkMiddleware> _middlewares = new List<INetworkMiddleware>();

        public void Add(INetworkMiddleware middleware)
        {
            if (middleware == null) throw new ArgumentNullException(nameof(middleware));
            _middlewares.Add(middleware);
        }

        /// <summary>
        /// 将中间件插入管线首部。适用于需要先于协议中间件处理全部流量的工具。
        /// </summary>
        public void AddFirst(INetworkMiddleware middleware)
        {
            if (middleware == null) throw new ArgumentNullException(nameof(middleware));
            _middlewares.Insert(0, middleware);
        }

        /// <summary>
        /// 从管线中移除指定中间件。用于运行时动态卸载（例如禁用网络调理模拟）。
        /// 若中间件不存在则无操作。
        /// </summary>
        /// <param name="middleware">要移除的中间件实例。</param>
        /// <returns>是否成功移除。</returns>
        public bool Remove(INetworkMiddleware middleware)
        {
            if (middleware == null) return false;
            return _middlewares.Remove(middleware);
        }

        /// <summary>
        /// 当前管线中的中间件数量。
        /// </summary>
        public int Count => _middlewares.Count;

        public void ProcessInbound(ISessionContext context, NetworkPacketHeader header, ArraySegment<byte> payload, Action<NetworkPacketHeader, ArraySegment<byte>> terminal)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (terminal == null) throw new ArgumentNullException(nameof(terminal));

            InvokeInbound(0, context, header, payload, terminal);
        }

        public void ProcessOutbound(ISessionContext context, NetworkPacketHeader header, ArraySegment<byte> payload, Action<NetworkPacketHeader, ArraySegment<byte>> terminal)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (terminal == null) throw new ArgumentNullException(nameof(terminal));

            InvokeOutbound(0, context, header, payload, terminal);
        }

        private void InvokeInbound(int index, ISessionContext context, NetworkPacketHeader header, ArraySegment<byte> payload, Action<NetworkPacketHeader, ArraySegment<byte>> terminal)
        {
            if (index >= _middlewares.Count)
            {
                terminal(header, payload);
                return;
            }

            _middlewares[index].OnInbound(context, header, payload, (h, p) => InvokeInbound(index + 1, context, h, p, terminal));
        }

        private void InvokeOutbound(int index, ISessionContext context, NetworkPacketHeader header, ArraySegment<byte> payload, Action<NetworkPacketHeader, ArraySegment<byte>> terminal)
        {
            if (index >= _middlewares.Count)
            {
                terminal(header, payload);
                return;
            }

            _middlewares[index].OnOutbound(context, header, payload, (h, p) => InvokeOutbound(index + 1, context, h, p, terminal));
        }
    }
}
