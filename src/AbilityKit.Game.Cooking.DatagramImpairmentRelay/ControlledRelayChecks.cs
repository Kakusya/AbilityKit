using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;

namespace AbilityKit.Game.Cooking.DatagramImpairmentRelay;

internal static class ControlledRelayChecks
{
    internal static void Run()
    {
        using var backend=Open();using var client=Open();using var wrong=Open();
        var directory=Path.Combine(Path.GetTempPath(),"cooking-relay-controls",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        var nonce=Guid.NewGuid().ToString();var report=Path.Combine(directory,"relay.json");
        using var process=new Process{StartInfo=new ProcessStartInfo("dotnet"){
            UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true}};
        foreach(var a in new[]{Assembly.GetExecutingAssembly().Location,"--nonce",nonce,"--profile","P3","--repeat","1","--backend-port",((IPEndPoint)backend.LocalEndPoint!).Port.ToString(),"--report",report})process.StartInfo.ArgumentList.Add(a);
        if(!process.Start())throw new InvalidOperationException("Control child start");
        var stderr=process.StandardError.ReadToEndAsync();long sequence=0;var events=new List<JsonElement>();var watch=Stopwatch.StartNew();
        void Send(string kind,int? route=null,IPEndPoint? source=null){process.StandardInput.WriteLine(JsonSerializer.Serialize(new{nonce,sequence=++sequence,kind,route,address=source?.Address.ToString(),port=source?.Port,clientPid=Environment.ProcessId}));process.StandardInput.Flush();}
        JsonElement Event(string kind){
            for(;;){var found=events.FindIndex(e=>e.GetProperty("kind").GetString()==kind);if(found>=0){var e=events[found];events.RemoveAt(found);return e.GetProperty("data");}
                if(watch.Elapsed.TotalSeconds>30)throw new TimeoutException("Control child30s");
                var task=process.StandardOutput.ReadLineAsync();if(!task.Wait(TimeSpan.FromSeconds(5)))throw new TimeoutException("Control event5s");var line=task.Result??throw new InvalidOperationException("Control child EOF");
                File.AppendAllText(Path.Combine(directory,"stdout.log"),line+Environment.NewLine);if(!line.StartsWith("CONTROL_EVENT "))continue;
                using var document=JsonDocument.Parse(line[14..]);var value=document.RootElement.Clone();if(value.GetProperty("nonce").GetString()!=nonce||value.GetProperty("pid").GetInt32()!=process.Id)throw new InvalidOperationException("Control provenance");events.Add(value);
            }
        }
        void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        try{
            var first=Event("ROUTE_READY");var front=IPEndPoint.Parse(first.GetProperty("frontend").GetString()!);var upstream=IPEndPoint.Parse(first.GetProperty("upstream").GetString()!);
            client.SendTo(new byte[]{1,2,3},front);var candidate=Event("SOURCE_CANDIDATE");Check(candidate.GetProperty("port").GetInt32()==((IPEndPoint)client.LocalEndPoint!).Port,"Actual candidate");
            Send("AllowSource",1,(IPEndPoint)client.LocalEndPoint!);Event("SOURCE_ALLOWED");var received=Receive(backend);Check(received.Bytes.SequenceEqual(new byte[]{1,2,3})&&received.Source.Equals(upstream),"Buffered bootstrap raw forward");
            backend.SendTo(new byte[]{4,5},upstream);Check(Receive(client).Bytes.SequenceEqual(new byte[]{4,5}),"Raw return");wrong.SendTo(new byte[]{9,9,9,9},front);
            Send("Arm");Event("ARMED");client.SendTo(new byte[]{6},front);client.SendTo(new byte[]{7},front);
            // Wait actual delivery before Off: this checks the configured delayed queue, then drain acknowledgement.
            var a=Receive(backend);var b=Receive(backend);Check(new[]{a.Bytes[0],b.Bytes[0]}.Order().SequenceEqual(new byte[]{6,7}),"Delayed unchanged raw delivery");
            Send("Off");Event("OFF");Event("OFF_DRAINED");Send("RetireRoute",1);Event("RETIRED");Send("PrepareRoute");var second=Event("ROUTE_READY");Check(first.GetProperty("frontend").GetString()!=second.GetProperty("frontend").GetString()&&first.GetProperty("upstream").GetString()!=second.GetProperty("upstream").GetString(),"Retired port nonreuse");
            Send("Stop");Check(process.WaitForExit(5000)&&process.ExitCode==0,"Actual relay child zero exit");
            using var final=JsonDocument.Parse(File.ReadAllText(report));var root=final.RootElement;Check(root.GetProperty("passed").GetBoolean()&&root.GetProperty("queued").GetInt32()==0&&root.GetProperty("unverifiedCount").GetInt32()==0,"Actual final drain");Check(root.GetProperty("wrongSources").GetInt64()==1&&root.GetProperty("wrongSourceBytes").GetInt64()==4,"Actual wrong-source byte/count");
        }finally{if(!process.HasExited){process.Kill();process.WaitForExit();}File.WriteAllText(Path.Combine(directory,"stderr.log"),stderr.GetAwaiter().GetResult());Console.WriteLine("Relay control evidence "+directory);}
        foreach(var scenario in new[]{"wrong-nonce","wrong-sequence","unknown-control","active-route-cap"}){
            var childNonce=Guid.NewGuid().ToString();var childReport=Path.Combine(directory,scenario+".json");
            using var child=new Process{StartInfo=new ProcessStartInfo("dotnet"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true}};
            foreach(var argument in new[]{Assembly.GetExecutingAssembly().Location,"--nonce",childNonce,"--backend-port",((IPEndPoint)backend.LocalEndPoint!).Port.ToString(),"--report",childReport})child.StartInfo.ArgumentList.Add(argument);
            Check(child.Start(),"Negative control child start");var outTask=child.StandardOutput.ReadToEndAsync();var errTask=child.StandardError.ReadToEndAsync();
            try{
                void Write(long seq,string kind,string? token=null){child.StandardInput.WriteLine(JsonSerializer.Serialize(new{nonce=token??childNonce,sequence=seq,kind}));child.StandardInput.Flush();}
                switch(scenario){case "wrong-nonce":Write(1,"Arm",Guid.NewGuid().ToString());break;case "wrong-sequence":Write(2,"Arm");break;case "unknown-control":Write(1,"Unknown");break;default:Write(1,"PrepareRoute");Write(2,"PrepareRoute");break;}
                Check(child.WaitForExit(5000)&&child.ExitCode==1,"Actual bounded rejection "+scenario);using var rejected=JsonDocument.Parse(File.ReadAllText(childReport));Check(!rejected.RootElement.GetProperty("passed").GetBoolean(),"Rejection report "+scenario);
            }finally{if(!child.HasExited){child.Kill();child.WaitForExit();}File.WriteAllText(Path.Combine(directory,scenario+".stdout.log"),outTask.GetAwaiter().GetResult());File.WriteAllText(Path.Combine(directory,scenario+".stderr.log"),errTask.GetAwaiter().GetResult());}
        }
    }
    private static Socket Open(){var s=new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp);s.Bind(new IPEndPoint(IPAddress.Loopback,0));s.ReceiveTimeout=5000;return s;}
    private static (byte[] Bytes,IPEndPoint Source) Receive(Socket socket){var bytes=new byte[65535];EndPoint source=new IPEndPoint(IPAddress.Any,0);var n=socket.ReceiveFrom(bytes,ref source);return (bytes.AsSpan(0,n).ToArray(),(IPEndPoint)source);}
}
