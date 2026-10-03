using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;

namespace AbilityKit.Game.Cooking.DatagramImpairmentRelay;

internal static class ClosedPortControls
{
    internal static void Run()
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("Actual Windows UDP10054 closed-port control NOT_VERIFIED on this OS.");
        One(true);One(false);One(false,true);
    }
    private static void One(bool declared,bool upstreamFault=false)
    {
        using var backend=Open();using var client=Open();using var successor=Open();
        var directory=Path.Combine(Path.GetTempPath(),"cooking-relay-closed-port-controls",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        var nonce=Guid.NewGuid().ToString();var report=Path.Combine(directory,"relay.json");
        using var child=new Process{StartInfo=new ProcessStartInfo("dotnet"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true}};
        foreach(var a in new[]{Assembly.GetExecutingAssembly().Location,"--nonce",nonce,"--profile","P0","--backend-port",((IPEndPoint)backend.LocalEndPoint!).Port.ToString(),"--report",report})child.StartInfo.ArgumentList.Add(a);
        if(!child.Start())throw new InvalidOperationException("Closed-port child start");var stderr=child.StandardError.ReadToEndAsync();long sequence=0;var events=new List<JsonElement>();var clock=Stopwatch.StartNew();
        void Check(bool pass,string name){if(!pass)throw new InvalidOperationException(name);}
        void Send(string kind,int route=1,IPEndPoint? source=null){child.StandardInput.WriteLine(JsonSerializer.Serialize(new{nonce,sequence=++sequence,kind,route,address=source?.Address.ToString(),port=source?.Port,clientPid=Environment.ProcessId}));child.StandardInput.Flush();}
        JsonElement Event(string kind){for(;;){var found=events.FindIndex(e=>e.GetProperty("kind").GetString()==kind);if(found>=0){var value=events[found];events.RemoveAt(found);return value.GetProperty("data");}
            Check(clock.Elapsed.TotalSeconds<15,"Closed-port child15s");var task=child.StandardOutput.ReadLineAsync();Check(task.Wait(TimeSpan.FromSeconds(5)),"Actual closed-port event5s NOT_VERIFIED");var line=task.Result??throw new InvalidOperationException("Closed-port child EOF");File.AppendAllText(Path.Combine(directory,"stdout.log"),line+Environment.NewLine);if(!line.StartsWith("CONTROL_EVENT "))continue;using var doc=JsonDocument.Parse(line[14..]);var e=doc.RootElement.Clone();Check(e.GetProperty("nonce").GetString()==nonce&&e.GetProperty("pid").GetInt32()==child.Id,"Closed-port owned event");events.Add(e);}}
        try{
            var route=Event("ROUTE_READY");var front=IPEndPoint.Parse(route.GetProperty("frontend").GetString()!);var upstream=IPEndPoint.Parse(route.GetProperty("upstream").GetString()!);var candidate=(IPEndPoint)client.LocalEndPoint!;
            client.SendTo(new byte[]{1},front);Event("SOURCE_CANDIDATE");Send("AllowSource",source:candidate);Event("SOURCE_ALLOWED");Receive(backend);
            Send("Arm");Event("ARMED");Send("Off");Event("OFF");Event("OFF_DRAINED");JsonElement? close=null;
            if(declared){Send("DeclareClientClose");close=Event("CLIENT_CLOSE_DECLARED");}
            if(upstreamFault){backend.Dispose();client.SendTo(new byte[]{8,9},front);}else{client.Dispose();backend.SendTo(new byte[]{8,9},upstream);}
            if(declared){
                var notification=Event("CLOSE_TRANSPORT_NOTIFICATION");Check(notification.GetProperty("nativeCode").GetInt32()==10054&&notification.GetProperty("socket").GetString()=="frontend"&&notification.GetProperty("notificationPayloadBytes").ValueKind==JsonValueKind.Null&&notification.GetProperty("handledCloseNotification").GetBoolean(),"Actual declared Windows notification");
                Check(notification.GetProperty("timestamp").GetInt64()<=close!.Value.GetProperty("deadline").GetInt64(),"Actual declared deadline");Send("RetireRoute");Event("RETIRED");Send("PrepareRoute");var next=Event("ROUTE_READY");var front2=IPEndPoint.Parse(next.GetProperty("frontend").GetString()!);var upstream2=IPEndPoint.Parse(next.GetProperty("upstream").GetString()!);Check(!front2.Equals(front)&&!upstream2.Equals(upstream),"Actual isolated successor route");
                successor.SendTo(new byte[]{3,4},front2);Event("SOURCE_CANDIDATE");Send("AllowSource",2,(IPEndPoint)successor.LocalEndPoint!);Event("SOURCE_ALLOWED");Check(Receive(backend).SequenceEqual(new byte[]{3,4}),"Actual new-route request after notification");backend.SendTo(new byte[]{5,6},upstream2);Check(Receive(successor).SequenceEqual(new byte[]{5,6}),"Actual new-route response after notification");Send("Stop");Check(child.WaitForExit(5000)&&child.ExitCode==0,"Declared close real zero exit");
            }else Check(child.WaitForExit(5000)&&child.ExitCode==1,"Undeclared/frontend or genuine closed backend actual reset stays fatal");
            using var parsed=JsonDocument.Parse(File.ReadAllText(report));var final=parsed.RootElement;Check(final.GetProperty("passed").GetBoolean()==declared,"Actual close outcome");
            var faults=final.GetProperty("socketFaults");Check(faults.GetArrayLength()>0&&faults[0].GetProperty("nativeCode").GetInt32()==10054&&faults[0].GetProperty("handledCloseNotification").GetBoolean()==declared,"Actual unexpected/handled transport metadata");Check(faults[0].GetProperty("socket").GetString()==(upstreamFault?"upstream":"frontend"),"Actual fault socket identity");
            foreach(var counter in final.GetProperty("counters").EnumerateArray()){var values=counter.GetProperty("values");Check(values.GetProperty("received").GetInt64()==values.GetProperty("forwarded").GetInt64()&&values.GetProperty("receivedBytes").GetInt64()==values.GetProperty("forwardedBytes").GetInt64()&&values.GetProperty("intentionallyDropped").GetInt64()==0,"Notification did not fabricate raw/drop counts");}
            Check(final.GetProperty("handledCloseNotifications").GetInt64()==(declared?1:0),"Distinct notification count");
        }finally{if(!child.HasExited){child.Kill();child.WaitForExit();}File.WriteAllText(Path.Combine(directory,"stderr.log"),stderr.GetAwaiter().GetResult());File.AppendAllText(Path.Combine(directory,"stdout.log"),child.StandardOutput.ReadToEnd());Console.WriteLine("Closed-port control evidence declared="+declared+" upstream="+upstreamFault+" "+directory);}
    }
    private static Socket Open(){var s=new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp);s.Bind(new IPEndPoint(IPAddress.Loopback,0));s.ReceiveTimeout=5000;return s;}
    private static byte[] Receive(Socket socket){var b=new byte[65535];EndPoint source=new IPEndPoint(IPAddress.Any,0);var n=socket.ReceiveFrom(b,ref source);return b.AsSpan(0,n).ToArray();}
}
