using System.Diagnostics;
using System.Net;
using System.Text.Json;
using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.EtRuntime;
using AbilityKit.Game.Cooking.Session;
using AbilityKit.Game.Cooking.NetworkAcceptance;
using AbilityKit.Game.Cooking.NetworkProcessMeasurement;
using AbilityKit.Game.Cooking.ImpairmentControl;
using AbilityKit.Game.Cooking.NetworkImpairmentMeasurement;
using AbilityKit.Network.Transport.LiteNet;

string Option(string name,string fallback){var i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
if(args.Contains("--retry-conservation-controls"))return RetryConservation.Controls(Option("--report","retry-conservation-controls.json"));
var role=args.FirstOrDefault()??"";var nonce=Option("--nonce","");var profile=Option("--profile","P0");var repeat=int.Parse(Option("--repeat","1"));
var output=Path.GetFullPath(Option("--report",role+".json"));var mailbox=new ControlMailbox(nonce);var overall=Stopwatch.StartNew();object? evidence=null;
return SingleThreadOwner.Run(async()=>{try{Require(Guid.TryParse(nonce,out _)&&repeat is >=1 and <=3,"Provenance");evidence=role=="host"?await Host():role=="client"?await Client():throw new ArgumentException("Role");Save(true,null);return 0;}catch(Exception e){Save(false,e.ToString());Console.Error.WriteLine(e);return 1;}});
void Save(bool passed,string? failure){Directory.CreateDirectory(Path.GetDirectoryName(output)!);File.WriteAllText(output,JsonSerializer.Serialize(new{passed,failure,nonce,profile,repeat,role,pid=Environment.ProcessId,machine=Environment.MachineName,stopwatchFrequency=Stopwatch.Frequency,etMvid=typeof(CookingLevelEtHost).Assembly.ManifestModule.ModuleVersionId,sessionMvid=typeof(CookingNetworkSessionHost).Assembly.ManifestModule.ModuleVersionId,evidence,physicalTwoPc="NOT_VERIFIED",performanceTarget="UNSET",topology="SameMachineMixedLocalFramedRemoteUdpRawRelay"},new JsonSerializerOptions{WriteIndented=true}));}
async Task<JsonElement> Control(string kind,Func<Task> pump){while(true){Require(overall.Elapsed.TotalSeconds<360,"Driver360s");if(mailbox.Take() is{} c){Require(c.GetProperty("kind").GetString()==kind,"Unexpected phase control");return c;}await pump();}}
async Task<object> Host(){
 var fixture=new ProcessMeasurementFixture();using var host=fixture.CreateHost();var adapter=new CookingNetworkAuthorityAdapter(host);
 using var session=new CookingNetworkSessionHost(adapter,new LiteNetChannelListener(IPAddress.Loopback,0,"abilitykit-cooking-v3"),new Dictionary<PlayerId,string>{{ProcessMeasurementFixture.Local,"local-credential"},{ProcessMeasurementFixture.Remote,"remote-credential"}});session.Start();
 using var local=new CookingNetworkSessionClient(ProcessMeasurementFixture.Local,"local-credential",session.CreateLocalClientTransport);var admitted=new HashSet<RecipeCommandId>();
 async Task Pump(){Require(overall.Elapsed.TotalSeconds<360,"Host360s");var frame=session.ProcessOwnerFrame();if(frame is not null)foreach(var a in frame.Admissions.Where(a=>a.Accepted))admitted.Add(a.CommandId);await Task.Delay(10);}
 async Task Wait(Func<bool> done,int seconds){var w=Stopwatch.StartNew();while(!done()&&w.Elapsed.TotalSeconds<seconds)await Pump();Require(done(),"Host phase deadline");}
 bool Marker(string id)=>session.LatestCapture?.FullRecipe?.Deduplication.Any(d=>d.Command==CookingNetworkWireCodec.DomainId(session.ServerSessionInstance,ProcessMeasurementFixture.Scope,ProcessMeasurementFixture.Remote,id)&&d.Outcome==CookingRecipeOutcome.Accepted)==true;
 var connect=local.ConnectAsync("inprocess",1);await Wait(()=>connect.IsCompleted,15);await connect;ControlMailbox.Event(nonce,"HOST_READY",new{backendPort=session.Port});
 await Wait(()=>Marker("preflight-drop")&&session.LatestSessionProjection.Participants.All(p=>p.Ready&&p.ConnectedOwnerBinding&&!p.CleanupPending),120);
 ControlMailbox.Event(nonce,"WAIT_LOAD",new{instance=session.ServerSessionInstance,configuration=fixture.Configuration.Identity.ToString(),generation=1,scope=ProcessMeasurementFixture.Scope,sessionProjection=session.LatestSessionProjection});await Control("BEGIN_LOAD",Pump);
 async Task<CookingNetworkWireResult> Send(CookingRecipeCommand c,string s)=>await local.SendCommandAsync(s,c).WaitAsync(TimeSpan.FromSeconds(15));
 var loadTask=RunLoad(0,()=>local.LatestBaseline!.State,Send,()=>local.IsSynchronized);await Wait(()=>loadTask.IsCompleted,110);LoadResult? load=null;string? sampleError=null;try{load=await loadTask;}catch(Exception error){sampleError=error.ToString();}
 if(load is not null)Require(load.IssuedDomainIds.Concat(load.WarmupIssuedDomainIds).All(id=>admitted.Contains(new(id))),"All local load IDs admitted");ControlMailbox.Event(nonce,"SAMPLE_DRAINED",new{healthySample=sampleError is null});
 await Control("EXPECT_RECOVERY",Pump);await Wait(()=>!session.LatestSessionProjection.Participants.Single(p=>p.Participant==ProcessMeasurementFixture.Remote).ConnectedOwnerBinding,30);ControlMailbox.Event(nonce,"REMOTE_CLOSED",new{instance=session.ServerSessionInstance});
 await Wait(()=>Marker("recovery-new-drop")&&session.LatestSessionProjection.Participants.All(p=>p.Ready&&p.ConnectedOwnerBinding&&!p.CleanupPending),90);
 Require(session.ApplyControl(new(CookingNetworkControlKind.Pause,"impairment-final-pause")).Accepted,"Trusted Pause");session.ProcessOwnerFrame();
 await Wait(()=>local.IsSynchronized&&local.LatestBaseline?.State.Observation.Lifecycle.State==CookingLevelState.Paused&&CookingNetworkWireCodec.Hash(local.LatestBaseline.State)==CookingNetworkWireCodec.Hash(session.LatestCapture)&&session.LatestSessionProjection.Participants.All(p=>p.Ready&&p.ConnectedOwnerBinding&&!p.CleanupPending),30);
 var final=adapter.CaptureFullState().State!;ValidateView(session.LatestSessionProjection,session.ServerSessionInstance,false);Require(fixture.CreateCount==1&&final.FullRecipe!.Settlements.Count==0,"One authority no fabricated Submit");
 var result=new{backendPort=session.Port,serverInstance=session.ServerSessionInstance,configurationIdentity=fixture.Configuration.Identity.ToString(),hash=CookingNetworkWireCodec.Hash(final),baselineHash=CookingNetworkWireCodec.BaselineHash(final,session.LatestSessionProjection),finalCapture=final,sessionProjection=session.LatestSessionProjection,localBaselineIdentity=local.LatestBaseline!.Identity,localIsSynchronized=local.IsSynchronized,finalBaselineSize=BaselineSize(local.LatestBaseline),authorityCreateCount=fixture.CreateCount,localLoad=load,healthySample=sampleError is null,sampleError,recoveryPassed=true,actuallyAdmittedDomainIds=admitted.Select(x=>x.Value).Order().ToArray(),diagnostics=session.Diagnostics};
 await Wait(()=>!session.LatestSessionProjection.Participants.Single(p=>p.Participant==ProcessMeasurementFixture.Remote).ConnectedOwnerBinding,30);return result;
}
async Task<object> Client(){
 var peer=new FramedFaultPeer();var routes=new List<int>();
 async Task Pump(){peer.Poll();Require(overall.Elapsed.TotalSeconds<360,"Client360s");await Task.Delay(2);}
 async Task Wait(Func<bool> done,int seconds=30){var w=Stopwatch.StartNew();while(!done()&&w.Elapsed.TotalSeconds<seconds)await Pump();peer.Poll();Require(done(),"Client phase deadline");}
 async Task<CookingNetworkWireResult> Send(CookingRecipeCommand c,string s){var t=peer.Queue(c,s);await Wait(()=>t.IsCompleted);return await t;}
 async Task<CookingNetworkWireResult> Execute(CookingRecipeCommand c,string s){var r=await Send(c,s);Require(r.Result?.Outcome==CookingRecipeOutcome.Accepted,"Real probe rejected "+JsonSerializer.Serialize(r));await Wait(()=>peer.Latest!.State.FullRecipe!.StateVersion>=r.Result!.StateVersion);return r;}
 try{
 var open=await Control("OPEN",()=>Task.Delay(2));var front=open.GetProperty("frontendPort").GetInt32();routes.Add(front);peer.Open("127.0.0.1",front);await Wait(()=>RunningReady(peer),30);
 await Execute(Toggle(peer.Latest!.State,1),"preflight-pickup");var original=Toggle(peer.Latest!.State,1);var receipt=await Execute(original,"preflight-drop");await Wait(()=>RunningReady(peer));
 var old=peer.Binding!;ControlMailbox.Event(nonce,"WAIT_LOAD",new{instance=old.ServerSessionInstance,configuration=peer.Latest!.State.Observation.Lifecycle.ConfigIdentity,generation=old.ConnectionGeneration,ack=peer.ReadyIdentity,currentBaselineIdentity=peer.Latest.Identity,currentValidatedBaselineHash=CookingNetworkWireCodec.BaselineHash(peer.Latest.State,peer.Latest.Session),scope=ProcessMeasurementFixture.Scope,ready=RunningReady(peer),frontendPort=front,preflightDomainId=receipt.DomainCommandId,preflightAcceptedVersion=receipt.Result!.StateVersion});await Control("BEGIN_LOAD",Pump);
 var task=RunLoad(1,()=>peer.Latest!.State,Send,()=>peer.Ready);while(!task.IsCompleted)await Pump();LoadResult? load=null;string? sampleError=null;try{load=await task;}catch(Exception error){sampleError=error.ToString();}ControlMailbox.Event(nonce,"SAMPLE_DRAINED",new{healthySample=sampleError is null});await Control("CLOSE_OLD",Pump);
 var beforeClose=peer.Latest!.State.FullRecipe!;peer.Dispose();ControlMailbox.Event(nonce,"OLD_CLOSED",new{old.ConnectionGeneration});var route=await Control("RECOVERY_ROUTE",()=>Task.Delay(2));front=route.GetProperty("frontendPort").GetInt32();Require(!routes.Contains(front),"Fresh proxy frontend");routes.Add(front);peer=new();peer.Open("127.0.0.1",front,old);await Wait(()=>RunningReady(peer),30);
 Require(peer.Binding!.ServerSessionInstance==old.ServerSessionInstance&&peer.Binding.ConnectionGeneration==old.ConnectionGeneration+1&&peer.Binding.RebindToken!=old.RebindToken,"Actual same-instance token rotation");
 var beforeBaseline=peer.Latest!;var before=beforeBaseline.State.FullRecipe!;CookingNetworkWireResult? replay=null;RetryConservation.Proof? retryProof=null;object? retryCaptureEvidence=null;
 try{
  var initialWatermark=beforeBaseline.Session.Participants.Single(p=>p.Participant==ProcessMeasurementFixture.Remote);
  Require(initialWatermark.ConnectionGeneration==2&&initialWatermark.LastValidatedClientSequence==0&&initialWatermark.LastTerminalClientSequence==0,"Actual new-generation pre-retry watermark0/0");
  Require(receipt.Result?.IsDuplicate==false&&receipt.DomainCommandId==CookingNetworkWireCodec.DomainId(old.ServerSessionInstance,ProcessMeasurementFixture.Scope,ProcessMeasurementFixture.Remote,"preflight-drop"),"Original real receipt/domain identity");
  // This is the new frozen peer's first Queue call: its real wire sequence is1, not the old receipt's state version.
  replay=await Send(original,"preflight-drop");
  Require(replay.Result!.IsDuplicate&&replay.DomainCommandId==receipt.DomainCommandId&&replay.Result.Outcome==receipt.Result!.Outcome&&replay.Result.Reason==receipt.Result.Reason&&replay.Result.StateVersion==receipt.Result.StateVersion&&JsonSerializer.Serialize(replay.Result.Supply)==JsonSerializer.Serialize(receipt.Result.Supply)&&replay.Result.Events.Count==0,"Exact cached original receipt");
  await Wait(()=>peer.Latest!.Identity.ServerSessionInstance==old.ServerSessionInstance&&peer.Latest.Identity.Participant==ProcessMeasurementFixture.Remote&&peer.Latest.Identity.Scope==ProcessMeasurementFixture.Scope&&peer.Latest.Identity.ConnectionGeneration==2&&peer.Latest.Identity.SnapshotSequence>beforeBaseline.Identity.SnapshotSequence&&peer.Latest.Session.Participants.Any(p=>p.Participant==ProcessMeasurementFixture.Remote&&p.ConnectionGeneration==2&&p.LastValidatedClientSequence==1&&p.LastTerminalClientSequence==1),30);
  retryProof=RetryConservation.Verify(before,peer.Latest!.State.FullRecipe!);
 }finally{
  var observed=peer.Latest!;retryCaptureEvidence=new{beforeBaseline,afterBaseline=observed,originalPayload=original,originalReceipt=receipt,replay,expectedWireSequence=1,proof=retryProof,fieldDifferences=RetryConservation.Differences(before,observed.State.FullRecipe!)};
  evidence=new{stage="cached-retry-conservation",retryCaptureEvidence};
 }
 if(peer.Latest!.State.FullRecipe!.Items.Single(i=>i.Id==ProcessMeasurementFixture.Tools[1]).Location.Kind==LocationKind.PlayerHand)await Execute(Toggle(peer.Latest.State,1),"recovery-normalize");await Execute(Toggle(peer.Latest!.State,1),"recovery-new-pickup");await Execute(Toggle(peer.Latest!.State,1),"recovery-new-drop");
 await Wait(()=>peer.Latest!.State.Observation.Lifecycle.State==CookingLevelState.Paused&&peer.ReadyIdentity==peer.Latest.Identity&&peer.Ready&&peer.Connected,30);var baseline=peer.Latest!;ValidateView(baseline.Session,peer.Binding.ServerSessionInstance,false);Require(baseline.Identity.StateHash==CookingNetworkWireCodec.BaselineHash(baseline.State,baseline.Session),"Complete final ACK");
 var result=new{serverInstance=peer.Binding.ServerSessionInstance,generation=peer.Binding.ConnectionGeneration,configurationIdentity=baseline.State.Observation.Lifecycle.ConfigIdentity,hash=CookingNetworkWireCodec.Hash(baseline.State),baselineHash=baseline.Identity.StateHash,finalBaseline=baseline,finalAckIdentity=peer.ReadyIdentity,sessionProjection=baseline.Session,connected=peer.Connected,ready=peer.Ready,finalBaselineSize=BaselineSize(baseline),frontendHistory=routes,cachedDuplicate=replay!.Result!.IsDuplicate,originalDomainId=receipt.DomainCommandId,replayedDomainId=replay.DomainCommandId,preCloseItemState=beforeClose.Items,postCleanupPreRetryItemState=before.Items,retryCaptureEvidence,droppedApplicationResponses=peer.Dropped,load,healthySample=sampleError is null,sampleError,recoveryPassed=true};
 ControlMailbox.Event(nonce,"FINAL_READY",new{hash=result.hash,baselineHash=result.baselineHash,ack=peer.ReadyIdentity,generation=peer.Binding.ConnectionGeneration,frontendPort=front,paused=true,ready=peer.Ready,connected=peer.Connected,timestamp=Stopwatch.GetTimestamp()});
 var hold=Stopwatch.StartNew();while(hold.Elapsed.TotalSeconds<5){await Pump();Require(peer.Connected&&peer.Ready,"Final hold disconnected");}return result;
 }finally{peer.Dispose();}
}

static bool RunningReady(FramedFaultPeer peer)=>peer.Ready&&peer.Connected&&peer.Binding is{} binding&&peer.ReadyIdentity is{} ack&&
 ack.ServerSessionInstance==binding.ServerSessionInstance&&ack.Participant==binding.Participant&&ack.ConnectionGeneration==binding.ConnectionGeneration&&ack.Scope==ProcessMeasurementFixture.Scope&&
 peer.Latest?.Session.Participants.Any(p=>p.Participant==binding.Participant&&p.ConnectionGeneration==binding.ConnectionGeneration&&p.Ready&&p.ConnectedOwnerBinding&&!p.CleanupPending)==true;
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
