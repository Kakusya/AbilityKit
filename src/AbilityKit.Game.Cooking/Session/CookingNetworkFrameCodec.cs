using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
namespace AbilityKit.Game.Cooking.Session;

internal sealed class CookingNetworkFrameCodec(int maximum) : IFrameCodec
{
    public IFrameDecoder CreateDecoder() => new Decoder(maximum);
    public ArraySegment<byte> Encode(NetworkPacketHeader header, ArraySegment<byte> payload) =>
        LengthPrefixedFrameCodec.Instance.Encode(header, payload);
    private sealed class Decoder(int maximum) : IFrameDecoder
    {
        private readonly NetworkFrameReader _reader = new() { MaxFrameLength = maximum };
        public void Reset() => _reader.Reset();
        public void Append(ArraySegment<byte> bytes) => _reader.Append(bytes);
        public bool TryRead(out NetworkPacketHeader header, out ArraySegment<byte> payload) => _reader.TryRead(out header, out payload);
    }
}
