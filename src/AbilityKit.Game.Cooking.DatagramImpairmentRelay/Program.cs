using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using AbilityKit.Game.Cooking.DatagramImpairmentRelay;
using AbilityKit.Game.Cooking.ImpairmentControl;

if(args.Contains("--self-check")){var optionIndex=Array.IndexOf(args,"--report");return RelayControls.Run(optionIndex>=0&&optionIndex+1<args.Length?args[optionIndex+1]:null);}
string Option(string name,string fallback){var i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
var nonce=Option("--nonce","");var profile=Option("--profile","P0");var repeat=int.Parse(Option("--repeat","1"));
var report=Path.GetFullPath(Option("--report","relay.json"));
var backend=new IPEndPoint(IPAddress.Loopback,int.Parse(Option("--backend-port","0")));
var controlMode=args.Contains("--control-mode");var countLimit=long.Parse(Option("--queue-count-limit","65536"));var byteLimit=long.Parse(Option("--queue-byte-limit","67108864"));var holdQueue=false;
var selected=Policy.For(profile);var policy=new Policy(0,0,0);var epoch=0;var mailbox=new ControlMailbox(nonce);
var watch=Stopwatch.StartNew();var routes=new List<Route>();var queue=new PriorityQueue<Packet,(long Due,long Index)>();
var counters=new Dictionary<(int Route,string Direction,int Epoch),Counters>();
var trace=new Queue<object>();long queuedBytes=0,highBytes=0,highCount=0,index=0,wrongSources=0,wrongSourceBytes=0,unverifiedBytes=0,unverifiedCount=0,sendWouldBlock=0;
long ingressDatagrams=0,ingressBytes=0,overflowDatagrams=0,overflowBytes=0;
long workingPeak=0,privatePeak=0;var allocated=GC.GetTotalAllocatedBytes();using var process=Process.GetCurrentProcess();var cpu=process.TotalProcessorTime;
var offDrainRequested=false;var stopped=false;string? failure=null;
try {
    if(!Guid.TryParse(nonce,out _)||repeat is <1 or >3||backend.Port==0)throw new ArgumentException("Invalid relay provenance.");
    if(countLimit is <1 or >65536||byteLimit is <1 or >67108864||!controlMode&&(countLimit!=65536||byteLimit!=67108864))throw new ArgumentException("Invalid control-only queue limits.");
    Prepare();
    while(!stopped){
        if(watch.Elapsed.TotalSeconds>420)throw new TimeoutException("Relay420s deadline.");
        if(mailbox.Take() is { } control){
            var kind=control.GetProperty("kind").GetString();
            switch(kind){
                case "PrepareRoute":Prepare();break;
                case "AllowSource":{
                    var route=routes.Single(r=>r.Id==control.GetProperty("route").GetInt32());
                    var source=new IPEndPoint(IPAddress.Parse(control.GetProperty("address").GetString()!),control.GetProperty("port").GetInt32());
                    if(route.Retired||route.Client is not null||!IPAddress.IsLoopback(source.Address)||route.Candidate?.Equals(source)!=true)
                        throw new InvalidOperationException("Unapproved source repin.");
                    route.Client=source;route.ClientPid=control.GetProperty("clientPid").GetInt32();if(route.ClientPid<=0)throw new InvalidOperationException("Client PID absent.");
                    // Wrapper proves actual owned PID/socket before this trusted network-only allow command.
                    foreach(var pending in route.Unverified){unverifiedCount--;unverifiedBytes-=pending.Bytes.Length;Schedule(route,"c2s",pending.Bytes,pending.Received);}
                    route.Unverified.Clear();ControlMailbox.Event(nonce,"SOURCE_ALLOWED",new{route=route.Id,client=source.ToString(),route.ClientPid});break;
                }
                case "Arm":if(epoch!=0||unverifiedCount!=0)throw new InvalidOperationException("Invalid Arm boundary.");epoch=1;policy=selected;ControlMailbox.Event(nonce,"ARMED",new{epoch,profile,priorEpochQueued=queue.Count,timestamp=Stopwatch.GetTimestamp()});break;
                case "Off":if(epoch!=1||unverifiedCount!=0)throw new InvalidOperationException("Invalid Off boundary.");epoch=2;policy=new(0,0,0);offDrainRequested=true;ControlMailbox.Event(nonce,"OFF",new{epoch,queued=queue.Count,timestamp=Stopwatch.GetTimestamp()});break;
                case "RetireRoute":{
                    var route=routes.Single(r=>r.Id==control.GetProperty("route").GetInt32());
                    if(queue.UnorderedItems.Any(p=>p.Element.Route==route)||route.Unverified.Count!=0)throw new InvalidOperationException("Retirement before drain.");
                    route.Retired=true;route.Front.Dispose();route.Upstream.Dispose();ControlMailbox.Event(nonce,"RETIRED",new{route=route.Id});break;
                }
                case "Stop":if(queue.Count!=0||routes.Any(r=>r.Unverified.Count!=0))throw new InvalidOperationException("Stop before drain.");stopped=true;break;
                case "HoldQueue":if(!controlMode)throw new InvalidOperationException("Control-only hold rejected.");holdQueue=true;ControlMailbox.Event(nonce,"HELD",new{epoch});break;
                case "ReleaseQueue":if(!controlMode)throw new InvalidOperationException("Control-only release rejected.");holdQueue=false;ControlMailbox.Event(nonce,"RELEASED",new{epoch});break;
                case "QueueStats":if(!controlMode)throw new InvalidOperationException("Control-only stats rejected.");ControlMailbox.Event(nonce,"QUEUE_STATS",new{epoch,holdQueue,queued=queue.Count,queuedBytes,unverifiedCount,unverifiedBytes,entries=queue.UnorderedItems.Take(16).Select(p=>new{p.Element.Epoch,p.Element.Due,p.Element.Received,route=p.Element.Route.Id,p.Element.Direction,destination=p.Element.Direction=="c2s"?backend.ToString():p.Element.Route.Client!.ToString(),sha=Convert.ToHexString(p.Element.Digest)})});break;
                default:throw new InvalidOperationException("Unknown trusted relay control.");
            }
        }
        if(stopped)break;
        foreach(var route in routes.Where(r=>!r.Retired)){
            Receive(route,route.Front,"c2s");Receive(route,route.Upstream,"s2c");
            if(route.Unverified.Any(p=>(Stopwatch.GetTimestamp()-p.Received)/(double)Stopwatch.Frequency>5))throw new TimeoutException("Unverified source5s.");
        }
        var now=Stopwatch.GetTimestamp();
        if(queue.UnorderedItems.Any(p=>(now-p.Element.Received)/(double)Stopwatch.Frequency>5))throw new TimeoutException("Datagram residence5s.");
        for(var quota=0;!holdQueue&&quota<256&&queue.TryPeek(out var packet,out var due)&&due.Due<=now;quota++){
            if((now-packet.Received)/(double)Stopwatch.Frequency>5)throw new TimeoutException("Datagram residence5s.");
            var socket=packet.Direction=="c2s"?packet.Route.Upstream:packet.Route.Front;
            var destination=packet.Direction=="c2s"?backend:packet.Route.Client!;
            int sent;try{sent=socket.SendTo(packet.Bytes,destination);}catch(SocketException e)when(e.SocketErrorCode==SocketError.WouldBlock){sendWouldBlock++;break;}
            if(sent!=packet.Bytes.Length||SHA256.HashData(packet.Bytes).AsSpan().SequenceEqual(packet.Digest)==false)throw new InvalidOperationException("Raw payload changed.");
            queue.Dequeue();queuedBytes-=packet.Bytes.Length;
            var count=counters[(packet.Route.Id,packet.Direction,packet.Epoch)];count.Forwarded++;count.ForwardedBytes+=sent;
            if(packet.DirectionIndex<count.LastForwardedIndex)count.Reordered++;count.LastForwardedIndex=Math.Max(count.LastForwardedIndex,packet.DirectionIndex);
            count.DueLateTicks+=Math.Max(0,now-packet.Due);count.ActualDelayTicks+=now-packet.Received;count.LargestDelayTicks=Math.Max(count.LargestDelayTicks,now-packet.Received);
        }
        if(offDrainRequested&&!queue.UnorderedItems.Any(p=>p.Element.Epoch<2)){
            offDrainRequested=false;ControlMailbox.Event(nonce,"OFF_DRAINED",new{epoch,timestamp=Stopwatch.GetTimestamp()});
        }
        process.Refresh();workingPeak=Math.Max(workingPeak,process.WorkingSet64);privatePeak=Math.Max(privatePeak,process.PrivateMemorySize64);
        Thread.Sleep(1);
    }
}catch(Exception error){failure=error.ToString();Console.Error.WriteLine(error);}
finally{
    foreach(var route in routes){route.Front.Dispose();route.Upstream.Dispose();}
    Directory.CreateDirectory(Path.GetDirectoryName(report)!);
    File.WriteAllText(report,JsonSerializer.Serialize(new{passed=failure is null,failure,nonce,profile,repeat,role="relay",pid=Environment.ProcessId,machine=Environment.MachineName,
        mvid=Assembly.GetExecutingAssembly().ManifestModule.ModuleVersionId,backend=backend.ToString(),stopwatchFrequency=Stopwatch.Frequency,epoch,
        routes=routes.Select(r=>new{r.Id,frontend=r.Frontend.ToString(),upstream=r.UpstreamEndpoint.ToString(),client=r.Client?.ToString(),r.ClientPid,r.Retired,
            seeds=new{c2s=Decisions.Seed(profile,repeat,r.Id,"c2s"),s2c=Decisions.Seed(profile,repeat,r.Id,"s2c")}}),
        counters=counters.Select(p=>new{route=p.Key.Route,direction=p.Key.Direction,epoch=p.Key.Epoch,values=p.Value.Snapshot()}),
        queued=queue.Count,queuedBytes,highCount,highBytes,unverifiedCount,unverifiedBytes,ingressDatagrams,ingressBytes,overflowDatagrams,overflowBytes,wrongSources,wrongSourceBytes,retiredSocketTraffic="NOT_OBSERVED: retired sockets are closed; OS discards are not counted",sendWouldBlock,trace=trace.ToArray(),traceTruncated=index>4096,
        processCpuMilliseconds=(process.TotalProcessorTime-cpu).TotalMilliseconds,allThreadAllocatedBytes=GC.GetTotalAllocatedBytes()-allocated,
        sampledPeakWorkingBytes=workingPeak,sampledPeakPrivateBytes=privatePeak,durationMilliseconds=watch.Elapsed.TotalMilliseconds,
        controlMode,holdQueue,limits=new{bytes=byteLimit,datagrams=countLimit,createdRoutes=4,activeRoutes=2,trace=4096,controlLines=64},
        metrics="Relay whole-process only; memory sampled each event-loop iteration. Raw UDP bytes include LiteNet protocol/retransmission. Policy is receipt epoch; Off does not undo prior loss/delay. Trace is bounded, counters complete; no packet payload dump.",
        topology="SameMachineMixedLocalFramedRemoteUdpRawRelay",physicalTwoPc="NOT_VERIFIED",performanceTarget="UNSET"},new JsonSerializerOptions{WriteIndented=true}));
}
return failure is null?0:1;

void Prepare(){
    if(routes.Count>=4||routes.Count(r=>!r.Retired)>=2)throw new InvalidOperationException("Route cap.");
    var route=new Route(routes.Count+1);
    var existingPorts=routes.SelectMany(r=>new[]{r.Frontend.Port,r.UpstreamEndpoint.Port}).Append(backend.Port).ToHashSet();
    if(existingPorts.Contains(route.Frontend.Port)||existingPorts.Contains(route.UpstreamEndpoint.Port)||route.Frontend.Port==route.UpstreamEndpoint.Port){route.Front.Dispose();route.Upstream.Dispose();throw new InvalidOperationException("Endpoint collision or retired port reuse.");}
    routes.Add(route);
    ControlMailbox.Event(nonce,"ROUTE_READY",new{route=route.Id,frontend=route.Frontend.ToString(),upstream=route.UpstreamEndpoint.ToString(),backend=backend.ToString()});
}
void Receive(Route route,Socket socket,string direction){
    for(var quota=0;quota<256&&socket.Poll(0,SelectMode.SelectRead);quota++){
        EndPoint source=new IPEndPoint(IPAddress.Any,0);var received=socket.ReceiveFrom(route.Buffer,ref source);var endpoint=(IPEndPoint)source;
        ingressDatagrams++;ingressBytes+=received;
        if(received>65507)throw new InvalidOperationException("Oversize UDP payload.");
        if(direction=="s2c"&&!endpoint.Equals(backend)||direction=="c2s"&&route.Client is not null&&!endpoint.Equals(route.Client)){wrongSources++;wrongSourceBytes+=received;continue;}
        var copy=route.Buffer.AsSpan(0,received).ToArray();var now=Stopwatch.GetTimestamp();
        if(direction=="c2s"&&route.Client is null){
            if(!IPAddress.IsLoopback(endpoint.Address)){wrongSources++;wrongSourceBytes+=received;continue;}
            if(route.Candidate is null){route.Candidate=endpoint;ControlMailbox.Event(nonce,"SOURCE_CANDIDATE",new{route=route.Id,address=endpoint.Address.ToString(),port=endpoint.Port});}
            if(!route.Candidate.Equals(endpoint)){wrongSources++;wrongSourceBytes+=received;continue;}
            if(route.Unverified.Count>=64||!QueueBudget.CanAdmit(queue.Count+unverifiedCount,queuedBytes+unverifiedBytes,received,countLimit,byteLimit)){overflowDatagrams++;overflowBytes+=received;throw new InvalidOperationException("Unverified bootstrap/global bound.");}
            route.Unverified.Add((copy,now));unverifiedCount++;unverifiedBytes+=received;highBytes=Math.Max(highBytes,queuedBytes+unverifiedBytes);highCount=Math.Max(highCount,queue.Count+unverifiedCount);continue;
        }
        if(direction=="s2c"&&route.Client is null)throw new InvalidOperationException("Backend reply before verified route.");
        Schedule(route,direction,copy,now);
    }
}
void Schedule(Route route,string direction,byte[] bytes,long received){
    var key=(route.Id,direction,epoch);if(!counters.TryGetValue(key,out var count))counters.Add(key,count=new());
    count.Received++;count.ReceivedBytes+=bytes.Length;count.MaximumPayload=Math.Max(count.MaximumPayload,bytes.Length);
    var routeIndex=direction=="c2s"?++route.C2s:++route.S2c;var decision=Decisions.For(policy,Decisions.Seed(profile,repeat,route.Id,direction),routeIndex);index++;
    if(index<=4096)trace.Enqueue(new{route=route.Id,direction,epoch,index=routeIndex,length=bytes.Length,decision.Drop,decision.Delay});
    if(decision.Drop){count.Dropped++;count.DroppedBytes+=bytes.Length;return;}
    if(!QueueBudget.CanAdmit(queue.Count+unverifiedCount,queuedBytes+unverifiedBytes,bytes.Length,countLimit,byteLimit)){overflowDatagrams++;overflowBytes+=bytes.Length;throw new InvalidOperationException("Scheduled datagram cap.");}
    var due=received+(long)(decision.Delay/1000.0*Stopwatch.Frequency);
    queue.Enqueue(new(route,direction,epoch,routeIndex,bytes,SHA256.HashData(bytes),received,due),(due,index));
    queuedBytes+=bytes.Length;highBytes=Math.Max(highBytes,queuedBytes+unverifiedBytes);highCount=Math.Max(highCount,queue.Count+unverifiedCount);
}
internal sealed class Route
{
    internal readonly int Id;internal readonly Socket Front,Upstream;internal readonly IPEndPoint Frontend,UpstreamEndpoint;
    internal readonly byte[] Buffer=new byte[65535];internal readonly List<(byte[] Bytes,long Received)> Unverified=new();
    internal IPEndPoint? Client,Candidate;internal int ClientPid;internal bool Retired;internal long C2s,S2c;
    internal Route(int id){Id=id;Front=Open();Upstream=Open();Frontend=(IPEndPoint)Front.LocalEndPoint!;UpstreamEndpoint=(IPEndPoint)Upstream.LocalEndPoint!;}
    private static Socket Open(){var socket=new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp);socket.Bind(new IPEndPoint(IPAddress.Loopback,0));socket.Blocking=false;return socket;}
}
internal sealed record Packet(Route Route,string Direction,int Epoch,long DirectionIndex,byte[] Bytes,byte[] Digest,long Received,long Due);
