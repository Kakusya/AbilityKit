using AbilityKit.Game.Cooking.EtRuntime;
using AbilityKit.Game.Cooking.Session;
using AbilityKit.Network.Host.InProcess;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using Xunit;
namespace AbilityKit.Game.Cooking.Tests;
[Collection(CookingEtHostTestCollection.Name)]
public sealed class CookingNetworkDeferredAckIsolationTests
{
    private sealed class Factory(CookingRecipeFixture fixture, bool fault) : ICookingLevelGameplayFactory
    {
        public CookingRecipeSimulation Create(CookingLevelScope scope,CookingConfigurationSnapshot configuration) { var kitchen=new CookingRecipeSimulation(fixture,fault ? new ThrowingAllocator() : null); if(fault) kitchen.AddItem(new("fault-food"),new("raw"),ItemLocation.Station(new("station"))); return kitchen; }
    }
    private sealed class ThrowingAllocator : ICookingProductIdAllocator { public ItemId GetProductId(long sequence)=>throw new IOException("deferred ACK actual allocation fault"); }
    [Theory]
    [InlineData("close")]
    [InlineData("supersede")]
    [InlineData("scope")]
    [InlineData("disposed")]
    [InlineData("faulted")]
    public void Deferred_exact_ack_is_isolated_across_binding_scope_or_authority_retirement(string transition)
    {
        var a=new PlayerId("a");var b=new PlayerId("b");var scope=new CookingLevelScope(new(new("s"),new("w"),new("m")),new(1),new("level"),1);
        var raw=new DefinitionId("raw");var product=new DefinitionId("product");var station=new StationSlotId("station");var caps=new HashSet<string>{"cook"};
        var items=new[]{new CookingItemDefinition(raw,caps),new(product,caps)};var stations=new[]{new CookingApplianceDefinition(station,new HashSet<string>{"heat"})};
        var recipes=new[]{new CookingRecipeDefinition(new("cook"),new[]{raw},product,new("heat"),"heat",transition=="faulted"?2:3)};
        var registry=new CookingConfigurationRegistry();Assert.True(registry.Submit(new(new[]{"heat"},items,stations,recipes)).Accepted);
        var fixture=new CookingRecipeFixture(scope.MatchScope,new[]{a,b}.ToDictionary(p=>p,p=>new CookingPlayerConfig(p,caps,new HashSet<string>{station.Value})),items.ToDictionary(i=>i.Id),stations.ToDictionary(s=>s.Station),recipes.ToDictionary(r=>r.Id));
        using var host=new CookingLevelEtHost(new CookingLevelLifecycle(scope,registry.Current!,new Factory(fixture,transition=="faulted")));
        Assert.True(host.Prepare(new(scope.Level,new("map"),new(new("layout"),new[]{station},Array.Empty<DefinitionId>()),registry.Current!.Identity)).Accepted);Assert.True(host.Start().Accepted);
        using var session=new CookingNetworkSessionHost(new CookingNetworkAuthorityAdapter(host),new InProcessChannelListener(),new Dictionary<PlayerId,string>{{a,"credential-a"},{b,"credential-b"}});session.Start();
        using var first=new ConnectionManager(session.CreateLocalClientTransport,new ConnectionOptions{EnableReconnect=false});var firstPackets=new List<CookingNetworkWireEnvelope>();
        first.ServerPushReceived+=(_,bytes)=>{Assert.True(CookingNetworkWireCodec.TryDecode(bytes.AsSpan(),new(),out var packet));firstPackets.Add(packet!);};
        void Send<T>(ConnectionManager peer,CookingNetworkMessageKind kind,string correlation,T value)=>peer.Send(CookingNetworkWireCodec.OpCode,new ArraySegment<byte>(CookingNetworkWireCodec.Encode(kind,correlation,value)),(ushort)NetworkPacketFlags.ServerPush);
        first.Open("inprocess",1);Send(first,CookingNetworkMessageKind.Join,"join",new CookingNetworkJoin(a,"credential-a",null,null));session.ProcessOwnerFrame();
        var initial=CookingNetworkWireCodec.Read<CookingNetworkBaseline>(Assert.Single(firstPackets,p=>p.Kind==CookingNetworkMessageKind.Baseline))!;
        Send(first,CookingNetworkMessageKind.BaselineAck,"first-ack",initial.Identity);session.ProcessOwnerFrame();
        if(transition=="faulted") { Assert.True(host.TryEnqueue(new(scope,new(scope.MatchScope,host.HostFrameSequence+1,b,new("trusted-start"),CookingRecipeOperation.StartProcess,Recipe:new("cook"),Item:new("fault-food"),Station:station,ExpectedItemVersion:1),"trusted","trusted-start")).Accepted); Assert.Equal(CookingRecipeOutcome.Accepted,Assert.Single(host.Tick().Dispositions).Result!.Outcome); Assert.False(host.IsFaulted); }
        Assert.True(session.ApplyControl(new(CookingNetworkControlKind.Pause,"pause")).Accepted);first.Dispose();session.ProcessOwnerFrame();
        Assert.True(session.LatestSessionProjection.Participants.Single(p=>p.Participant==a).CleanupPending);
        using var second=new ConnectionManager(session.CreateLocalClientTransport,new ConnectionOptions{EnableReconnect=false});var packets=new List<CookingNetworkWireEnvelope>();
        second.ServerPushReceived+=(_,bytes)=>{Assert.True(CookingNetworkWireCodec.TryDecode(bytes.AsSpan(),new(),out var packet));packets.Add(packet!);};
        second.Open("inprocess",1);Send(second,CookingNetworkMessageKind.Join,"second-join",new CookingNetworkJoin(b,"credential-b",null,null));session.ProcessOwnerFrame();
        var paused=CookingNetworkWireCodec.Read<CookingNetworkBaseline>(Assert.Single(packets,p=>p.Kind==CookingNetworkMessageKind.Baseline))!;
        var before=host.Observe().CanonicalText();var frame=host.HostFrameSequence;
        Send(second,CookingNetworkMessageKind.BaselineAck,"deferred-exact-ack",paused.Identity);session.ProcessOwnerFrame();
        Assert.DoesNotContain(packets,p=>p.Kind==CookingNetworkMessageKind.Ready);
        Assert.Equal(before,host.Observe().CanonicalText());Assert.Equal(frame,host.HostFrameSequence);
        Assert.False(session.LatestSessionProjection.Participants.Single(p=>p.Participant==b).Ready);
        Send(second,CookingNetworkMessageKind.BaselineAck,"duplicate-deferred-ack",paused.Identity);session.ProcessOwnerFrame();
        Assert.DoesNotContain(packets,p=>p.Kind==CookingNetworkMessageKind.Ready||p.Kind==CookingNetworkMessageKind.Rejected);
        Assert.Equal(before,host.Observe().CanonicalText());Assert.Equal(frame,host.HostFrameSequence);
        // An invalid identity must not replace the first retained exact ACK.
        Send(second,CookingNetworkMessageKind.BaselineAck,"wrong-identity",paused.Identity with { IssueId="not-issued" });session.ProcessOwnerFrame();
        Assert.Contains(packets,p=>p.CorrelationId=="wrong-identity"&&p.Kind==CookingNetworkMessageKind.Rejected);
        Assert.DoesNotContain(packets,p=>p.Kind==CookingNetworkMessageKind.Ready);
        var binding=CookingNetworkWireCodec.Read<CookingNetworkJoined>(Assert.Single(packets,p=>p.Kind==CookingNetworkMessageKind.Joined))!;
        if(transition=="faulted") {
            Assert.True(session.ApplyControl(new(CookingNetworkControlKind.Resume,"fault-resume")).Accepted);session.ProcessOwnerFrame();
            Assert.True(host.IsFaulted);Assert.All(session.LatestSessionProjection.Participants,p=>Assert.False(p.Ready));
            Assert.DoesNotContain(packets,p=>p.Kind==CookingNetworkMessageKind.Ready);
            Send(second,CookingNetworkMessageKind.BaselineAck,"after-fault",paused.Identity);session.ProcessOwnerFrame();
            Assert.DoesNotContain(packets,p=>p.Kind==CookingNetworkMessageKind.Ready);return;
        }
        if(transition=="disposed") {
            host.Dispose();session.ProcessOwnerFrame();
            Assert.All(session.LatestSessionProjection.Participants,p=>Assert.False(p.Ready));
            Send(second,CookingNetworkMessageKind.BaselineAck,"after-dispose",paused.Identity);session.ProcessOwnerFrame();
            Assert.DoesNotContain(packets,p=>p.Kind==CookingNetworkMessageKind.Ready);
            Assert.Contains(packets,p=>p.Kind==CookingNetworkMessageKind.Rejected);
            return;
        }
        if(transition=="scope") {
            // Trusted lifecycle transitions occur without an intervening owner frame, preserving the real deferred slot until scope retirement.
            Assert.True(host.Resume().Accepted);
            Assert.True(host.BeginEnd(CookingLevelOutcome.Success).Accepted);Assert.True(host.CompleteEnd().Accepted);
            Assert.True(host.CreateSuccessor(new("next"),2).Accepted);session.ProcessOwnerFrame();
            Assert.DoesNotContain(packets,p=>p.Kind==CookingNetworkMessageKind.Ready);
            Assert.False(session.LatestSessionProjection.Participants.Single(p=>p.Participant==b).Ready);
            Send(second,CookingNetworkMessageKind.BaselineAck,"old-scope",paused.Identity);session.ProcessOwnerFrame();
            Assert.DoesNotContain(packets,p=>p.Kind==CookingNetworkMessageKind.Ready);
            var current=CookingNetworkWireCodec.Read<CookingNetworkBaseline>(packets.Last(p=>p.Kind==CookingNetworkMessageKind.Baseline))!;
            Assert.NotEqual(paused.Identity.Scope,current.Identity.Scope);
            Send(second,CookingNetworkMessageKind.BaselineAck,"current-scope",current.Identity);session.ProcessOwnerFrame();
            Assert.Contains(packets,p=>p.CorrelationId=="current-scope"&&p.Kind==CookingNetworkMessageKind.Ready);
            return;
        }
        using var replacement=new ConnectionManager(session.CreateLocalClientTransport,new ConnectionOptions{EnableReconnect=false});var fresh=new List<CookingNetworkWireEnvelope>();
        replacement.ServerPushReceived+=(_,bytes)=>{Assert.True(CookingNetworkWireCodec.TryDecode(bytes.AsSpan(),new(),out var packet));fresh.Add(packet!);};
        if(transition=="close") { second.Dispose();session.ProcessOwnerFrame(); }
        replacement.Open("inprocess",1);Send(replacement,CookingNetworkMessageKind.Join,"replacement",new CookingNetworkJoin(b,"credential-b",binding.ServerSessionInstance,binding.RebindToken));session.ProcessOwnerFrame();
        var joined=CookingNetworkWireCodec.Read<CookingNetworkJoined>(Assert.Single(fresh,p=>p.Kind==CookingNetworkMessageKind.Joined))!;
        Assert.True(joined.ConnectionGeneration>binding.ConnectionGeneration);
        Assert.NotEqual(binding.RebindToken,joined.RebindToken);
        Assert.True(session.ApplyControl(new(CookingNetworkControlKind.Resume,"resume")).Accepted);session.ProcessOwnerFrame();
        Assert.DoesNotContain(fresh,p=>p.Kind==CookingNetworkMessageKind.Ready);
        Assert.DoesNotContain(packets,p=>p.Kind==CookingNetworkMessageKind.Ready);
        var latest=CookingNetworkWireCodec.Read<CookingNetworkBaseline>(fresh.Last(p=>p.Kind==CookingNetworkMessageKind.Baseline))!;
        Send(replacement,CookingNetworkMessageKind.BaselineAck,"new-exact",latest.Identity);session.ProcessOwnerFrame();
        Assert.Contains(fresh,p=>p.CorrelationId=="new-exact"&&p.Kind==CookingNetworkMessageKind.Ready);
        Assert.DoesNotContain(fresh,p=>p.CorrelationId=="deferred-exact-ack"||p.CorrelationId=="duplicate-deferred-ack");
        Assert.Empty(session.LatestCapture!.FullRecipe!.Deduplication);
    }
}
