using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AbilityKit.Game.Cooking.DatagramImpairmentRelay;

internal static class QueueBudget
{
    internal static bool CanAdmit(long count,long bytes,int incoming,long countLimit=65536,long byteLimit=64L*1024*1024)=>count<countLimit&&incoming>=0&&bytes<=byteLimit-incoming;
}
internal static class CloseNotificationPolicy
{
    internal static bool CanHandle(bool windows,bool frontend,int nativeCode,System.Net.Sockets.SocketError error,long declared,long deadline,long timestamp,int priorCount)=>
        windows&&frontend&&nativeCode==10054&&error==System.Net.Sockets.SocketError.ConnectionReset&&declared>0&&timestamp>=declared&&timestamp<=deadline&&priorCount is >=0 and <64;
}
internal sealed record Policy(int Delay, int Jitter, int LossBasisPoints)
{
    internal static Policy For(string profile) => profile switch {
        "P0" => new(0,0,0), "P1" => new(50,0,0), "P2" => new(150,0,0), "P3" => new(50,20,0),
        "P4" => new(0,0,100), "P5" => new(0,0,500), "P6" => new(150,20,500),
        _ => throw new ArgumentException("Unknown impairment profile.")
    };
}
internal static class Decisions
{
    internal static ulong Seed(string profile, int repeat, int route, string direction) =>
        BinaryPrimitives.ReadUInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            new[] { "cooking-relay-v1", profile, repeat.ToString(System.Globalization.CultureInfo.InvariantCulture),
                route.ToString(System.Globalization.CultureInfo.InvariantCulture), direction }))));
    internal static ulong Draw(ulong seed, long index, int draw)
    {
        unchecked {
            var z = seed + ((ulong)index * 2 + (ulong)draw + 1) * 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
    internal static int Bounded(ulong value, int bound) => (int)(((UInt128)value * (uint)bound) >> 64);
    internal static (bool Drop, int Delay) For(Policy policy, ulong seed, long index) =>
        (Bounded(Draw(seed,index,0),10000) < policy.LossBasisPoints,
            Math.Max(0,policy.Delay + (policy.Jitter == 0 ? 0 : Bounded(Draw(seed,index,1),policy.Jitter * 2 + 1) - policy.Jitter)));
}
internal sealed class Counters
{
    internal long Received, ReceivedBytes, Dropped, DroppedBytes, Forwarded, ForwardedBytes, Reordered;
    internal long LastForwardedIndex, DueLateTicks, ActualDelayTicks, LargestDelayTicks, MaximumPayload;
    internal object Snapshot() => new { received=Received,receivedBytes=ReceivedBytes,intentionallyDropped=Dropped,intentionallyDroppedBytes=DroppedBytes,
        forwarded=Forwarded,forwardedBytes=ForwardedBytes,reordered=Reordered,dueLateTicks=DueLateTicks,actualDelayTicks=ActualDelayTicks,
        largestDelayTicks=LargestDelayTicks,maximumPayload=MaximumPayload,realizedLossPercent=Received==0?0:100.0*Dropped/Received };
}
