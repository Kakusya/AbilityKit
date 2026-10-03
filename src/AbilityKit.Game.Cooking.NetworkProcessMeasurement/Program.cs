using System.Diagnostics;
using System.Net;
using System.Text.Json;
using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.EtRuntime;
using AbilityKit.Game.Cooking.Session;
using AbilityKit.Game.Cooking.NetworkAcceptance;
using AbilityKit.Game.Cooking.NetworkProcessMeasurement;
using AbilityKit.Network.Transport.LiteNet;

var role=args.FirstOrDefault()??"help";
string Option(string name,string fallback){var i=Array.IndexOf(args,name); return i>=0&&i+1<args.Length?args[i+1]:fallback;}
var address=Option("--ip","127.0.0.1"); var port=int.Parse(Option("--port","0"));
var output=Path.GetFullPath(Option("--report",role+".json")); var repeat=int.Parse(Option("--repeat","1"));
object? evidence=null;
const string FixtureId="cooking-two-process-submit-fault-load-v3";
return SingleThreadOwner.Run(Run);
async Task<int> Run()
{
    try {
        if(role=="host") evidence=await Host(); else if(role=="client") evidence=await Client(); else throw new ArgumentException("Expected host/client.");
        Save(true,null); return 0;
    }catch(Exception error){Save(false,error.ToString()); Console.Error.WriteLine(error); return 1;}
}
void Save(bool passed,string? failure)
{
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    File.WriteAllText(output,JsonSerializer.Serialize(new{passed,failure,fixture=FixtureId,role,repeat,machine=Environment.MachineName,pid=Environment.ProcessId,address,port,
        stopwatchFrequency=Stopwatch.Frequency,topology="SameMachineIndependentProcessesUdp",protocol=3,recipeSchema=5,levelFormat=8,performanceTarget="UNSET",physicalTwoPc="NOT_VERIFIED",
        etMvid=typeof(CookingLevelEtHost).Assembly.ManifestModule.ModuleVersionId,sessionMvid=typeof(CookingNetworkSessionHost).Assembly.ManifestModule.ModuleVersionId,evidence},new JsonSerializerOptions{WriteIndented=true}));
}
async Task<object> Host()
{
    var fixture=new ProcessMeasurementFixture(); using var host=fixture.CreateHost(); var adapter=new CookingNetworkAuthorityAdapter(host);
    using var session=new CookingNetworkSessionHost(adapter,new LiteNetChannelListener(IPAddress.Parse(address),port,"abilitykit-cooking-v3"),
        new Dictionary<PlayerId,string>{{ProcessMeasurementFixture.Local,"local-credential"},{ProcessMeasurementFixture.Remote,"remote-credential"}});
    session.Start(); port=session.Port;
    using var local=new CookingNetworkSessionClient(ProcessMeasurementFixture.Local,"local-credential",session.CreateLocalClientTransport);
    var admitted=new HashSet<RecipeCommandId>(); long ownerCalls=0,ownerAllocations=0,ownerTicks=0;
    var scenario=Stopwatch.StartNew();
    async Task Pump()
    {
        Require(scenario.Elapsed.TotalSeconds<240,"Host pair deadline.");
        var bytes=GC.GetAllocatedBytesForCurrentThread();var time=Stopwatch.GetTimestamp();
        var frame=session.ProcessOwnerFrame();ownerCalls++;ownerAllocations+=GC.GetAllocatedBytesForCurrentThread()-bytes;ownerTicks+=Stopwatch.GetTimestamp()-time;
        if(frame is not null) foreach(var a in frame.Admissions.Where(a=>a.Accepted)) admitted.Add(a.CommandId);
        await Task.Delay(10);
    }
    async Task Wait(Func<bool> done,int seconds){var watch=Stopwatch.StartNew();while(!done()&&watch.Elapsed.TotalSeconds<seconds)await Pump();Require(done(),"Host phase deadline.");}
    var connect=local.ConnectAsync("inprocess",1);await Wait(()=>connect.IsCompleted,15);await connect;
    Console.WriteLine($"READY {port} {Environment.ProcessId}");Console.Out.Flush();
    bool Marker(string stable)=>session.LatestCapture!.FullRecipe!.Deduplication.Any(d=>d.Command==CookingNetworkWireCodec.DomainId(session.ServerSessionInstance,ProcessMeasurementFixture.Scope,ProcessMeasurementFixture.Remote,stable)&&d.Outcome==CookingRecipeOutcome.Accepted);
    await Wait(()=>Marker("fault-retry-verified-drop"),120);
    var fault=session.LatestCapture!.FullRecipe!;
    Require(fault.Settlements.Count==1&&session.LatestCapture!.Observation.Recipe!.AcceptedOrders.Count==1,"Real Submit committed once.");
    var tombstones=fault.Items.Where(i=>i.Id==ProcessMeasurementFixture.Cup||i.IsProduct).ToArray();
    Require(tombstones.Length==2&&tombstones.All(i=>i.Removed),"Both consumed records present.");
    Require(session.LatestSessionProjection.Participants.Single(p=>p.Participant==ProcessMeasurementFixture.Remote).ConnectionGeneration==2,"Remote rebind generation2.");
    async Task<CookingNetworkWireResult> Send(CookingRecipeCommand command,string stable)=>await local.SendCommandAsync(stable,command).WaitAsync(TimeSpan.FromSeconds(15));
    var load=RunLoad(0,()=>local.LatestBaseline!.State,Send,()=>local.IsSynchronized);
    await Wait(()=>load.IsCompleted&&Marker("remote-load-finished-drop"),110);var localLoad=await load;
    Require(localLoad.IssuedDomainIds.Concat(localLoad.WarmupIssuedDomainIds).All(id=>admitted.Contains(new(id))),"Local issued IDs actually ET admitted.");
    var finalBefore=adapter.CaptureFullState().State!;
    Require(fixture.CreateCount==1&&finalBefore.FullRecipe!.Settlements.Count==1,"One authority, one settlement after load.");
    Require(tombstones.SequenceEqual(finalBefore.FullRecipe!.Items.Where(i=>i.Id==ProcessMeasurementFixture.Cup||i.IsProduct)),"Consumed records conserved after load.");
    // Trusted runner stage completion only; a wire marker does not grant production Pause permission.
    Require(session.ApplyControl(new(CookingNetworkControlKind.Pause,"final-load-pause")).Accepted,"Trusted final Pause.");session.ProcessOwnerFrame();
    await Wait(()=>local.IsSynchronized&&local.LatestBaseline?.State.Observation.Lifecycle.State==CookingLevelState.Paused&&
        CookingNetworkWireCodec.Hash(local.LatestBaseline.State)==CookingNetworkWireCodec.Hash(session.LatestCapture),15);

    var final=adapter.CaptureFullState().State!;
    Require(final.Observation.Lifecycle.State==CookingLevelState.Paused&&CookingNetworkWireCodec.Hash(final)==CookingNetworkWireCodec.Hash(session.LatestCapture),"Stable same-frame capture.");
    ValidateView(session.LatestSessionProjection,session.ServerSessionInstance,allowRemoteClosed:false);
    var remoteIds=admitted.Where(id=>final.FullRecipe!.Deduplication.Any(d=>d.Command==id&&d.Player==ProcessMeasurementFixture.Remote)).Select(id=>id.Value).Order().ToArray();
    var report=new{scope=final.Observation.Scope,serverInstance=session.ServerSessionInstance,hash=CookingNetworkWireCodec.Hash(final),baselineHash=CookingNetworkWireCodec.BaselineHash(final,session.LatestSessionProjection),
        finalBaselineSize=BaselineSize(local.LatestBaseline!),finalCapture=final,localBaselineIdentity=local.LatestBaseline!.Identity,localIsSynchronized=local.IsSynchronized,finalLifecycle=final.Observation.Lifecycle.State.ToString(),resumableCheckpointPresent=final.ResumableCheckpoint is not null,
        sessionProjection=session.LatestSessionProjection,fault=new{settlements=1,acceptedOrders=1,tombstones=2,remoteGeneration=2},localLoad,
        actuallyAdmittedDomainIds=admitted.Select(id=>id.Value).Order().ToArray(),remoteAdmittedDomainIds=remoteIds,
        authorityCreateCount=fixture.CreateCount,configurationIdentity=fixture.Configuration.Identity.ToString(),configuration=fixture.Configuration.Identity,ownerCalls,ownerManagedBytes=ownerAllocations,ownerMilliseconds=ownerTicks*1000.0/Stopwatch.Frequency,scenarioMilliseconds=scenario.Elapsed.TotalMilliseconds,
        diagnostics=session.Diagnostics,metricDefinitions="Owner counters cover entire pair including handshake/fault/warmup/drain; managed owner allocations include inline local client callbacks, exclude other-thread/native/await. Payload bytes and queue peak are cumulative. Host monotonic timings are receive/map/result-send; not client RTT. No performance threshold."};
    // Capture provenance before remote exits; keep only this owned session alive for its final ACK/report.
    // The client reports from its acknowledged final gate before closing. Keep this owned host alive until that actual close,
    // rather than racing two fixed-duration holds. Post-gate paused connection cleanup is not part of captured consensus.
    await Wait(()=>!session.LatestSessionProjection.Participants.Single(p=>p.Participant==ProcessMeasurementFixture.Remote).ConnectedOwnerBinding,30);
    return report;
}
async Task<object> Client()
{
    var overall=Stopwatch.StartNew(); var peer=new FramedFaultPeer("lost-submit");
    async Task Wait(Func<bool> done,int seconds=30)
    {
        var watch=Stopwatch.StartNew();while(!done()&&watch.Elapsed.TotalSeconds<seconds){peer.Poll();Require(overall.Elapsed.TotalSeconds<240,"Client pair deadline.");await Task.Delay(2);}peer.Poll();Require(done(),"Client phase deadline.");
    }
    async Task<CookingNetworkWireResult> Send(CookingRecipeCommand command,string stable){var task=peer.Queue(command,stable);await Wait(()=>task.IsCompleted);return await task;}
    async Task<CookingNetworkWireResult> Execute(CookingRecipeCommand command,string stable){var r=await Send(command,stable);Require(r.Result?.Outcome==CookingRecipeOutcome.Accepted,"Real command rejected: "+JsonSerializer.Serialize(new{command,result=r}));await Wait(()=>peer.Latest!.State.FullRecipe!.StateVersion>=r.Result!.StateVersion);return r;}
    try {
        peer.Open(address,port);await Wait(()=>peer.Ready,15);
        await Execute(Action(peer.Latest!.State,CookingRecipeOperation.StartProcess,ProcessMeasurementFixture.Input,ProcessMeasurementFixture.Board),"fault-start");
        await Wait(()=>peer.Latest!.State.FullRecipe!.Processes.Count==0);
        var product=peer.Latest!.State.FullRecipe!.Items.Single(i=>i.IsProduct).Id;
        await Execute(Action(peer.Latest.State,CookingRecipeOperation.Pickup,product),"fault-product");
        await Execute(Action(peer.Latest.State,CookingRecipeOperation.PutIn,product,container:ProcessMeasurementFixture.Cup),"fault-plate");
        await Execute(Action(peer.Latest.State,CookingRecipeOperation.Pickup,ProcessMeasurementFixture.Cup),"fault-cup");
        await Execute(Action(peer.Latest.State,CookingRecipeOperation.BindOrder,product,order:ProcessMeasurementFixture.Order),"fault-bind");
        var submit=Action(peer.Latest.State,CookingRecipeOperation.SubmitOrder,product,order:ProcessMeasurementFixture.Order);
        var dropped=peer.Queue(submit,"stable-submit","lost-submit");
        await Wait(()=>peer.Dropped==1&&peer.Latest!.State.FullRecipe!.Settlements.Count==1&&peer.ReadyIdentity==peer.Latest.Identity);
        Require(!dropped.IsCompleted,"The chosen response was genuinely lost.");
        var committed=peer.Latest!.State.FullRecipe!;var consumed=committed.Items.Where(i=>i.Id==product||i.Id==ProcessMeasurementFixture.Cup).ToArray();
        Require(consumed.Length==2&&consumed.All(i=>i.Removed),"Real Submit tombstones reached baseline.");
        var old=peer.Binding!;peer.Dispose();peer=new();peer.Open(address,port,old);await Wait(()=>peer.Ready,15);
        Require(peer.Binding!.ServerSessionInstance==old.ServerSessionInstance&&peer.Binding.ConnectionGeneration==old.ConnectionGeneration+1&&peer.Binding.RebindToken!=old.RebindToken,"Exact live rebind.");
        var replay=await Execute(submit,"stable-submit");var after=peer.Latest!.State.FullRecipe!;
        Require(replay.Result!.IsDuplicate&&replay.DomainCommandId==CookingNetworkWireCodec.DomainId(old.ServerSessionInstance,ProcessMeasurementFixture.Scope,ProcessMeasurementFixture.Remote,"stable-submit"),"Cached original mapped Submit.");
        Require(consumed.SequenceEqual(after.Items.Where(i=>i.Id==product||i.Id==ProcessMeasurementFixture.Cup))&&committed.Settlements.SequenceEqual(after.Settlements)&&committed.Deduplication.SequenceEqual(after.Deduplication),"Exact business conservation after lost-result retry.");
        await Marker("fault-retry-verified-drop");
        var loadTask=RunLoad(1,()=>peer.Latest!.State,Send,()=>peer.Ready);
        while(!loadTask.IsCompleted){peer.Poll();Require(overall.Elapsed.TotalSeconds<240,"Load client deadline.");await Task.Delay(2);}var load=await loadTask;
        await Marker("remote-load-finished-drop");
        await Wait(()=>peer.Latest!.State.Observation.Lifecycle.State==CookingLevelState.Paused&&peer.ReadyIdentity==peer.Latest.Identity&&peer.Ready&&peer.Connected,30);
        // Continue poll so the exact final paused baseline ACK/Ready is actually observed.
        peer.Poll();
        var baseline=peer.Latest!;Require(baseline.Identity.StateHash==CookingNetworkWireCodec.BaselineHash(baseline.State,baseline.Session),"Final full baseline verified.");
        ValidateView(baseline.Session,peer.Binding.ServerSessionInstance,allowRemoteClosed:false);
        var finalHold=Stopwatch.StartNew();while(finalHold.Elapsed.TotalSeconds<5){peer.Poll();Require(peer.Connected&&peer.Ready,"Final ACK hold disconnected.");await Task.Delay(2);}
        return new{scope=baseline.Identity.Scope,serverInstance=peer.Binding.ServerSessionInstance,generation=peer.Binding.ConnectionGeneration,
            hash=CookingNetworkWireCodec.Hash(baseline.State),baselineHash=baseline.Identity.StateHash,sessionProjection=baseline.Session,
            configurationIdentity=baseline.State.Observation.Lifecycle.ConfigIdentity,finalBaselineSize=BaselineSize(baseline),finalBaseline=baseline,finalAckIdentity=peer.ReadyIdentity,connected=peer.Connected,ready=peer.Ready,finalLifecycle=baseline.State.Observation.Lifecycle.State.ToString(),resumableCheckpointPresent=baseline.State.ResumableCheckpoint is not null,
            fault=new{droppedResponses=1,cachedDuplicate=true,originalDomainId=replay.DomainCommandId,settlements=after.Settlements.Count,tombstones=consumed.Length,receiptCount=after.Deduplication.Count},load,
            scenarioMilliseconds=overall.Elapsed.TotalMilliseconds};
        async Task Marker(string stable){if(peer.Latest!.State.FullRecipe!.Items.Single(i=>i.Id==ProcessMeasurementFixture.Tools[1]).Location.Kind==LocationKind.PlayerHand) await Execute(Toggle(peer.Latest.State,1),stable+"-normalize"); await Execute(Toggle(peer.Latest!.State,1),stable+"-pickup");await Execute(Toggle(peer.Latest!.State,1),stable);}
    }finally{peer.Dispose();}
}
static CookingRecipeCommand Action(CookingNetworkAuthorityCapture state,CookingRecipeOperation op,ItemId? item=null,StationSlotId? station=null,ItemId? container=null,OrderId? order=null)=>
    new(ProcessMeasurementFixture.Match,0,ProcessMeasurementFixture.Remote,new("unused"),op,Item:item,Station:station,Container:container,Order:order,
        ExpectedItemVersion:item is null?0:state.FullRecipe!.Items.Single(i=>i.Id==item).Version);
static CookingRecipeCommand Toggle(CookingNetworkAuthorityCapture state,int player)
{
    var item=state.FullRecipe!.Items.Single(i=>i.Id==ProcessMeasurementFixture.Tools[player]);var hand=item.Location.Kind==LocationKind.PlayerHand;
    return new(ProcessMeasurementFixture.Match,0,player==0?ProcessMeasurementFixture.Local:ProcessMeasurementFixture.Remote,new("unused"),hand?CookingRecipeOperation.Drop:CookingRecipeOperation.Pickup,
        Item:item.Id,ExpectedItemVersion:item.Version,WorldAnchor:hand?item.Id.Value:null);
}
static void ValidateView(CookingNetworkSessionProjection view,string instance,bool allowRemoteClosed)
{
    Require(view.ServerSessionInstance==instance&&view.Participants.Count==2&&view.Participants.Select(p=>p.Participant).ToHashSet().SetEquals(new[]{ProcessMeasurementFixture.Local,ProcessMeasurementFixture.Remote}),"Exactly configured participants.");
    foreach(var p in view.Participants)Require(!p.CleanupPending&&p.ConnectionGeneration==(p.Participant==ProcessMeasurementFixture.Local?1:2)&&
        (allowRemoteClosed&&p.Participant==ProcessMeasurementFixture.Remote||p.ConnectedOwnerBinding&&p.Ready),"Ready/connection/generation provenance.");
}
static async Task<LoadResult> RunLoad(int player,Func<CookingNetworkAuthorityCapture> read,Func<CookingRecipeCommand,string,Task<CookingNetworkWireResult>> send,Func<bool> ready)
{
    var watch=Stopwatch.StartNew();using var process=Process.GetCurrentProcess();var cpu=process.TotalProcessorTime;
    var samples=new List<CookingNetworkWireResult>();var rtt=new List<double>();var issuedIds=new List<string>();var warmupIds=new List<string>();var offered=0;var busy=0;var late=0;var warmup=0;var warmupIssued=0;var warmupAccepted=0;var warmupBusy=0;var warmupLate=0;var completedAfterWindow=0;long sampledPeakWorkingSet=0,sampledPeakPrivateBytes=0;Task? flight=null;
    for(var i=0;i<350;i++){
        var due=i*200;while(watch.ElapsedMilliseconds<due)await Task.Delay(1);var measured=i>=50;
        process.Refresh();sampledPeakWorkingSet=Math.Max(sampledPeakWorkingSet,process.WorkingSet64);sampledPeakPrivateBytes=Math.Max(sampledPeakPrivateBytes,process.PrivateMemorySize64);
        if(measured)offered++;else warmup++;
        if(watch.ElapsedMilliseconds>=due+200){if(measured)late++;else warmupLate++;continue;}
        if(flight is{IsCompleted:false}){if(measured)busy++;else warmupBusy++;continue;}if(flight is not null)await flight;
        Require(ready(),"Load client not Ready.");var command=Toggle(read(),player);var stable=$"load-p{player}-{i}";
        if(!measured)warmupIssued++;
        flight=Complete();
        async Task Complete(){var started=Stopwatch.GetTimestamp();var result=await send(command,stable);Require(result.Result?.Outcome==CookingRecipeOutcome.Accepted,"Legal load command rejected: "+JsonSerializer.Serialize(new{command,result}));Require(!result.Result!.IsDuplicate,"Fresh load ID must not be a cached replay.");
            var elapsed=(Stopwatch.GetTimestamp()-started)*1000.0/Stopwatch.Frequency;var wait=Stopwatch.StartNew();
            while(read().FullRecipe!.StateVersion<result.Result!.StateVersion&&wait.Elapsed.TotalSeconds<30)await Task.Delay(1);Require(wait.Elapsed.TotalSeconds<30,"Full committed projection deadline.");
            if(measured){samples.Add(result);rtt.Add(elapsed);issuedIds.Add(result.DomainCommandId!.Value.Value);if(watch.ElapsedMilliseconds>=70000)completedAfterWindow++;}else {warmupAccepted++;warmupIds.Add(result.DomainCommandId!.Value.Value);}}
    }
    while(watch.ElapsedMilliseconds<70000)await Task.Delay(1);
    if(flight is not null)await flight;
    Require(offered==300&&warmup==50&&samples.Count+busy+late==offered&&warmupIssued==warmupAccepted&&warmupIssued+warmupBusy+warmupLate==warmup,"Offered schedule accounting.");
    return new(player,offered,warmup,samples.Count,busy,late,samples.Count,0,0,0,samples.ToArray(),issuedIds.ToArray(),warmupIds.ToArray(),Distribution(rtt),warmupIssued,warmupAccepted,warmupBusy,warmupLate,completedAfterWindow,sampledPeakWorkingSet,sampledPeakPrivateBytes,
        (process.TotalProcessorTime-cpu).TotalMilliseconds,watch.Elapsed.TotalMilliseconds,"Closed loop one inflight; offered5/s is not accepted guarantee. Results/RTT cohort uses offered index and can drain after70s. Memory peaks sampled at350 offered instants (5Hz), not OS lifetime peak. CPU is this process during load including warmup/drain; no crossprocess timestamp subtraction or performance threshold.");
}
static object BaselineSize(CookingNetworkBaseline baseline)
{
    var bytes=CookingNetworkWireCodec.Encode(CookingNetworkMessageKind.Baseline,"measured-final",baseline);
    var reader=new Utf8JsonReader(bytes,new JsonReaderOptions{MaxDepth=32});long tokens=0;while(reader.Read())tokens++;
    return new{bytes=bytes.Length,tokens,byteLimit=8*1024*1024,tokenLimit=1048576,collectionLimit=16384};
}
static object Distribution(IEnumerable<double> values){var a=values.Order().ToArray();double Q(double q)=>a.Length==0?0:a[(int)Math.Ceiling(q*a.Length)-1];return new{samples=a.Length,p50=Q(.5),p95=Q(.95),p99=Q(.99)};}
static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
internal sealed record LoadResult(int Player,int Offered,int WarmupOffered,int Issued,int SkippedBackpressure,int SchedulerSkipped,int Accepted,int Rejected,int Cancelled,int Pending,
    IReadOnlyList<CookingNetworkWireResult> Results,IReadOnlyList<string> IssuedDomainIds,IReadOnlyList<string> WarmupIssuedDomainIds,object RttMs,int WarmupIssued,int WarmupAccepted,int WarmupSkippedBackpressure,int WarmupSchedulerSkipped,int SampleCompletedAfterWindow,long SampledPeakWorkingSetBytes,long SampledPeakPrivateBytes,double ProcessCpuMilliseconds,double DurationMilliseconds,string MetricDefinitions);
