using AbilityKit.Game.Cooking.EtRuntime;
using AbilityKit.Game.Cooking.Session;
using AbilityKit.Network.Host.InProcess;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using Xunit;
namespace AbilityKit.Game.Cooking.Tests;
[Collection(CookingEtHostTestCollection.Name)]
public sealed class CookingNetworkPausedPublicationTests
{
    private sealed class Factory(CookingRecipeFixture fixture) : ICookingLevelGameplayFactory
    {
        public CookingRecipeSimulation Create(CookingLevelScope scope,CookingConfigurationSnapshot configuration)=>new(fixture);
    }
    [Fact]
    public void Old_pending_ack_releases_one_current_paused_baseline_without_ticks_or_same_state_ack_flood()
    {
        var player=new PlayerId("paused-peer");var scope=new CookingLevelScope(new(new("session"),new("world"),new("match")),new(1),new("level"),1);
        var raw=new DefinitionId("raw");var cooked=new DefinitionId("cooked");var station=new StationSlotId("stove");
        var caps=new HashSet<string>{"cook"};var items=new[]{new CookingItemDefinition(raw,caps),new(cooked,caps)};
        var stations=new[]{new CookingApplianceDefinition(station,new HashSet<string>{"heat"})};
        var recipes=new[]{new CookingRecipeDefinition(new("cook"),new[]{raw},cooked,new("heat"),"heat",3)};
        var registry=new CookingConfigurationRegistry();Assert.True(registry.Submit(new(new[]{"heat"},items,stations,recipes)).Accepted);
        var fixture=new CookingRecipeFixture(scope.MatchScope,new Dictionary<PlayerId,CookingPlayerConfig>{{player,new(player,caps,new HashSet<string>{station.Value})}},
            items.ToDictionary(i=>i.Id),stations.ToDictionary(s=>s.Station),recipes.ToDictionary(r=>r.Id));
        using var host=new CookingLevelEtHost(new CookingLevelLifecycle(scope,registry.Current!,new Factory(fixture)));
        Assert.True(host.Prepare(new(scope.Level,new("map"),new(new("layout"),new[]{station},Array.Empty<DefinitionId>()),registry.Current!.Identity)).Accepted);Assert.True(host.Start().Accepted);
        using var session=new CookingNetworkSessionHost(new CookingNetworkAuthorityAdapter(host),new InProcessChannelListener(),new Dictionary<PlayerId,string>{{player,"credential"}});session.Start();
        using var peer=new ConnectionManager(session.CreateLocalClientTransport,new ConnectionOptions{EnableReconnect=false});var packets=new List<CookingNetworkWireEnvelope>();
        peer.ServerPushReceived+=(_,bytes)=>{Assert.True(CookingNetworkWireCodec.TryDecode(bytes.AsSpan(),new(),out var envelope));packets.Add(envelope!);};
        void Send<T>(CookingNetworkMessageKind kind,string correlation,T value)=>peer.Send(CookingNetworkWireCodec.OpCode,new ArraySegment<byte>(CookingNetworkWireCodec.Encode(kind,correlation,value)),(ushort)NetworkPacketFlags.ServerPush);
        CookingNetworkBaseline[] Baselines()=>packets.Where(p=>p.Kind==CookingNetworkMessageKind.Baseline).Select(p=>CookingNetworkWireCodec.Read<CookingNetworkBaseline>(p)!).ToArray();
        peer.Open("inprocess",1);Send(CookingNetworkMessageKind.Join,"join",new CookingNetworkJoin(player,"credential",null,null));session.ProcessOwnerFrame();
        var first=Assert.Single(Baselines());Send(CookingNetworkMessageKind.BaselineAck,"first-ack",first.Identity);session.ProcessOwnerFrame();
        Assert.Equal(2,Baselines().Length);var held=Baselines()[1];Assert.Equal(CookingLevelState.Running,held.State.Observation.Lifecycle.State);
        Assert.True(session.ApplyControl(new(CookingNetworkControlKind.Pause,"pause")).Accepted);Assert.Equal(2,Baselines().Length);
        var before=host.Observe().CanonicalText();var checkpoint=host.CaptureReadOnlyFullState().State!.FullRecipe!.CanonicalText();var frame=host.HostFrameSequence;
        Send(CookingNetworkMessageKind.BaselineAck,"old-held-ack",held.Identity);session.ProcessOwnerFrame();
        Assert.Equal(3,Baselines().Length);var paused=Baselines()[2];Assert.Equal(CookingLevelState.Paused,paused.State.Observation.Lifecycle.State);
        Assert.Equal(host.Observe().CanonicalText(),paused.State.Observation.CanonicalText());Assert.Null(paused.State.ResumableCheckpoint);
        Assert.Equal(before,host.Observe().CanonicalText());Assert.Equal(checkpoint,host.CaptureReadOnlyFullState().State!.FullRecipe!.CanonicalText());Assert.Equal(frame,host.HostFrameSequence);
        Send(CookingNetworkMessageKind.BaselineAck,"paused-ack",paused.Identity);session.ProcessOwnerFrame();
        for(var i=0;i<12;i++)session.ProcessOwnerFrame();
        Assert.Equal(3,Baselines().Length);Assert.Equal(before,host.Observe().CanonicalText());Assert.Equal(frame,host.HostFrameSequence);
    }

    [Fact]
    public void Exact_ack_during_paused_cleanup_is_retained_until_resume_and_does_not_strand_ready()
    {
        var a=new PlayerId("a");var b=new PlayerId("b");var scope=new CookingLevelScope(new(new("s"),new("w"),new("m")),new(1),new("level"),1);
        var raw=new DefinitionId("raw");var product=new DefinitionId("product");var station=new StationSlotId("station");var caps=new HashSet<string>{"cook"};
        var items=new[]{new CookingItemDefinition(raw,caps),new(product,caps)};var stations=new[]{new CookingApplianceDefinition(station,new HashSet<string>{"heat"})};
        var recipes=new[]{new CookingRecipeDefinition(new("cook"),new[]{raw},product,new("heat"),"heat",3)};
        var registry=new CookingConfigurationRegistry();Assert.True(registry.Submit(new(new[]{"heat"},items,stations,recipes)).Accepted);
        var fixture=new CookingRecipeFixture(scope.MatchScope,new[]{a,b}.ToDictionary(p=>p,p=>new CookingPlayerConfig(p,caps,new HashSet<string>{station.Value})),items.ToDictionary(i=>i.Id),stations.ToDictionary(s=>s.Station),recipes.ToDictionary(r=>r.Id));
        using var host=new CookingLevelEtHost(new CookingLevelLifecycle(scope,registry.Current!,new Factory(fixture)));
        Assert.True(host.Prepare(new(scope.Level,new("map"),new(new("layout"),new[]{station},Array.Empty<DefinitionId>()),registry.Current!.Identity)).Accepted);Assert.True(host.Start().Accepted);
        using var session=new CookingNetworkSessionHost(new CookingNetworkAuthorityAdapter(host),new InProcessChannelListener(),new Dictionary<PlayerId,string>{{a,"credential-a"},{b,"credential-b"}});session.Start();
        using var first=new ConnectionManager(session.CreateLocalClientTransport,new ConnectionOptions{EnableReconnect=false});var firstPackets=new List<CookingNetworkWireEnvelope>();
        first.ServerPushReceived+=(_,bytes)=>{Assert.True(CookingNetworkWireCodec.TryDecode(bytes.AsSpan(),new(),out var packet));firstPackets.Add(packet!);};
        void Send<T>(ConnectionManager peer,CookingNetworkMessageKind kind,string correlation,T value)=>peer.Send(CookingNetworkWireCodec.OpCode,new ArraySegment<byte>(CookingNetworkWireCodec.Encode(kind,correlation,value)),(ushort)NetworkPacketFlags.ServerPush);
        first.Open("inprocess",1);Send(first,CookingNetworkMessageKind.Join,"join",new CookingNetworkJoin(a,"credential-a",null,null));session.ProcessOwnerFrame();
        var initial=CookingNetworkWireCodec.Read<CookingNetworkBaseline>(Assert.Single(firstPackets,p=>p.Kind==CookingNetworkMessageKind.Baseline))!;
        Send(first,CookingNetworkMessageKind.BaselineAck,"first-ack",initial.Identity);session.ProcessOwnerFrame();
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
        Assert.True(session.ApplyControl(new(CookingNetworkControlKind.Resume,"resume")).Accepted);session.ProcessOwnerFrame();
        Assert.Contains(packets,p=>p.Kind==CookingNetworkMessageKind.Ready&&CookingNetworkWireCodec.Read<CookingNetworkBaselineIdentity>(p)==paused.Identity);
        Assert.True(session.LatestSessionProjection.Participants.Single(p=>p.Participant==b).Ready);
        Assert.False(session.LatestSessionProjection.Participants.Single(p=>p.Participant==a).CleanupPending);
        Assert.True(packets.Count(p=>p.Kind==CookingNetworkMessageKind.Baseline)>1);
    }
}
