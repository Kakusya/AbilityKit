using AbilityKit.Game.Cooking.Session;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Game.Cooking.NetworkRichRecoveryAcceptance;

// MaxFrameLength is the advertised body (header + payload), excluding the four-byte prefix.
internal sealed class RichFrameCodec : IFrameCodec
{
    public static int MaximumBodyBytes => checked(new CookingNetworkSessionOptions().FrameBytes + 64);
    public IFrameDecoder CreateDecoder() => new Decoder();
    public ArraySegment<byte> Encode(NetworkPacketHeader header, ArraySegment<byte> payload) =>
        LengthPrefixedFrameCodec.Instance.Encode(header, payload);
    private sealed class Decoder : IFrameDecoder
    {
        private readonly NetworkFrameReader _reader = new() { MaxFrameLength = MaximumBodyBytes };
        public void Reset() => _reader.Reset();
        public void Append(ArraySegment<byte> bytes) => _reader.Append(bytes);
        public bool TryRead(out NetworkPacketHeader header, out ArraySegment<byte> payload) => _reader.TryRead(out header, out payload);
    }
}
