using System.Collections.Concurrent;
using AbilityKit.Game.Cooking.Session;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Transport.LiteNet;
namespace AbilityKit.Game.Cooking.NetworkProcessMeasurement;
// Application-only receive suppression. No UDP packet-loss or client authority is claimed.
internal sealed class FramedFaultPeer : IDisposable
{
    private readonly ConnectionManager _connection=new(()=>new LiteNetTransport("abilitykit-cooking-v3"),new ConnectionOptions{EnableReconnect=false});
    private readonly ConcurrentQueue<CookingNetworkWireEnvelope> _incoming=new();
    private readonly Dictionary<string,TaskCompletionSource<CookingNetworkWireResult>> _pending=new(StringComparer.Ordinal);
    private readonly string? _drop;
    private int _dropped;
    private long _sequence;
    private CookingNetworkBaselineIdentity? _acked;
    private CookingNetworkJoin? _joinToSend;
    internal CookingNetworkJoined? Binding {get;private set;}
    internal CookingNetworkBaseline? Latest {get;private set;}
    internal bool Ready {get;private set;}
    internal bool Connected=>_connection.IsConnected;
    internal CookingNetworkBaselineIdentity? ReadyIdentity {get;private set;}
    internal int Dropped=>Volatile.Read(ref _dropped);
    internal FramedFaultPeer(string? drop=null)
    {
        _drop=drop;
        _connection.ServerPushReceived+=(_,bytes)=>{
            if(!CookingNetworkWireCodec.TryDecode(bytes.AsSpan(),new(),out var envelope) || envelope is null) return;
            if(envelope.Kind==CookingNetworkMessageKind.CommandResult && envelope.CorrelationId==_drop && Interlocked.CompareExchange(ref _dropped,1,0)==0) return;
            _incoming.Enqueue(envelope);
        };
    }
    internal void Open(string address,int port,CookingNetworkJoined? old=null)
    {
        _joinToSend=new CookingNetworkJoin(ProcessMeasurementFixture.Remote,"remote-credential",old?.ServerSessionInstance,old?.RebindToken);
        _connection.Open(address,port);
    }
    private void Send<T>(CookingNetworkMessageKind kind,string correlation,T value)=>_connection.Send(CookingNetworkWireCodec.OpCode,
        new ArraySegment<byte>(CookingNetworkWireCodec.Encode(kind,correlation,value)),(ushort)NetworkPacketFlags.ServerPush);
    internal void Poll()
    {
        if(_joinToSend is { } join && Connected) { _joinToSend=null; Send(CookingNetworkMessageKind.Join,"join",join); }
        while(_incoming.TryDequeue(out var envelope)) switch(envelope.Kind) {
            case CookingNetworkMessageKind.Joined:
                Binding=CookingNetworkWireCodec.Read<CookingNetworkJoined>(envelope) ?? throw new InvalidOperationException("Invalid Joined.");
                if(Binding.Participant!=ProcessMeasurementFixture.Remote || Binding.ConnectionGeneration<=0) throw new InvalidOperationException("Invalid binding.");
                break;
            case CookingNetworkMessageKind.Baseline:
                var baseline=CookingNetworkWireCodec.Read<CookingNetworkBaseline>(envelope) ?? throw new InvalidOperationException("Invalid full baseline.");
                var id=baseline.Identity;
                if(Binding is null || id.ServerSessionInstance!=Binding.ServerSessionInstance || id.Participant!=Binding.Participant || id.ConnectionGeneration!=Binding.ConnectionGeneration ||
                    baseline.LevelFormatVersion!=CookingLevelCheckpointCodec.CurrentFormatVersion || baseline.RecipeSchemaVersion!=5 || id.Scope!=ProcessMeasurementFixture.Scope || id.Scope!=baseline.State.Observation.Scope ||
                    id.Scope!=baseline.State.Observation.Lifecycle.Scope || id.Epoch!=id.Scope.LevelEpoch ||
                    baseline.State.FullRecipe is null || baseline.State.FullRecipe.LevelScope!=id.Scope || baseline.State.FullRecipe.Scope!=id.Scope.MatchScope ||
                    id.StateHash!=CookingNetworkWireCodec.BaselineHash(baseline.State,baseline.Session) || baseline.Session.ServerSessionInstance!=id.ServerSessionInstance ||
                    !baseline.Session.Participants.Any(p=>p.Participant==id.Participant && p.ConnectedOwnerBinding && p.ConnectionGeneration==id.ConnectionGeneration) ||
                    Latest is not null && id.SnapshotSequence<=Latest.Identity.SnapshotSequence) throw new InvalidOperationException("Baseline identity/hash/scope mismatch.");
                Latest=CookingNetworkWireCodec.Freeze(baseline); _acked=id;
                Send(CookingNetworkMessageKind.BaselineAck,"ack-"+id.SnapshotSequence,id); break;
            case CookingNetworkMessageKind.Ready:
                var ack=CookingNetworkWireCodec.Read<CookingNetworkBaselineIdentity>(envelope);
                if(ack==_acked) {Ready=true;ReadyIdentity=ack;} break;
            case CookingNetworkMessageKind.CommandResult:
            case CookingNetworkMessageKind.Rejected:
                var result=CookingNetworkWireCodec.Read<CookingNetworkWireResult>(envelope) ?? throw new InvalidOperationException("Malformed result.");
                if(result.Reason is "AuthorityFaulted" or "Disposed" or "Busy" or "FullStateExceedsWireBounds") {Ready=false; throw new InvalidOperationException("Authority unavailable: "+result.Reason);}
                if(_pending.Remove(envelope.CorrelationId,out var completion)) completion.TrySetResult(result);
                else if(envelope.CorrelationId=="join") throw new InvalidOperationException("Join rejected: "+result.Reason);
                break;
        }
    }
    internal Task<CookingNetworkWireResult> Queue(CookingRecipeCommand command,string stable,string? correlation=null)
    {
        Poll(); if(!Ready || Binding is null) throw new InvalidOperationException("Not Ready.");
        var seq=++_sequence; correlation??="request-"+seq;
        var completion=new TaskCompletionSource<CookingNetworkWireResult>(TaskCreationOptions.RunContinuationsAsynchronously); _pending.Add(correlation,completion);
        Send(CookingNetworkMessageKind.Command,correlation,new CookingNetworkWireCommand(Binding.ServerSessionInstance,Binding.ConnectionGeneration,seq,stable,ProcessMeasurementFixture.Scope,command));
        return completion.Task;
    }
    public void Dispose()
    {
        Ready=false; _connection.Dispose();
        foreach(var pending in _pending.Values) pending.TrySetCanceled(); _pending.Clear();
    }
}
