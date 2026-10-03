using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;

namespace AbilityKit.Game.Cooking.DatagramImpairmentRelay;

internal static class ControlledQueueChecks
{
    internal static void Run()
    {
        foreach(var scenario in new[]{"count-cap","byte-cap","off-pending"})RunOne(scenario);
    }
    private static void RunOne(string scenario)
    {
        using var backend=Open();using var client=Open();
        var directory=Path.Combine(Path.GetTempPath(),"cooking-relay-queue-controls",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);var report=Path.Combine(directory,"relay.json");var nonce=Guid.NewGuid().ToString();
        using var process=new Process{StartInfo=new ProcessStartInfo("dotnet"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true}};
        var countLimit=scenario=="byte-cap"?8:2;var byteLimit=scenario=="byte-cap"?2:16;
        foreach(var a in new[]{Assembly.GetExecutingAssembly().Location,"--nonce",nonce,"--profile","P2","--backend-port",((IPEndPoint)backend.LocalEndPoint!).Port.ToString(),"--report",report,"--control-mode","--queue-count-limit",countLimit.ToString(),"--queue-byte-limit",byteLimit.ToString()})process.StartInfo.ArgumentList.Add(a);
        if(!process.Start())throw new InvalidOperationException("Queue child start");var stderr=process.StandardError.ReadToEndAsync();long sequence=0;var events=new List<JsonElement>();var watch=Stopwatch.StartNew();
        void Check(bool value,string message){if(!value)throw new InvalidOperationException(scenario+": "+message);}
        void Send(string kind,IPEndPoint? source=null){process.StandardInput.WriteLine(JsonSerializer.Serialize(new{nonce,sequence=++sequence,kind,route=1,address=source?.Address.ToString(),port=source?.Port,clientPid=Environment.ProcessId}));process.StandardInput.Flush();}
        JsonElement Event(string kind){for(;;){var found=events.FindIndex(e=>e.GetProperty("kind").GetString()==kind);if(found>=0){var value=events[found];events.RemoveAt(found);return value.GetProperty("data");}
            Check(watch.Elapsed.TotalSeconds<10,"Whole control10s");var task=process.StandardOutput.ReadLineAsync();Check(task.Wait(TimeSpan.FromSeconds(5)),"Event5s");var line=task.Result??throw new InvalidOperationException("Queue child EOF");File.AppendAllText(Path.Combine(directory,"stdout.log"),line+Environment.NewLine);if(!line.StartsWith("CONTROL_EVENT "))continue;using var document=JsonDocument.Parse(line[14..]);var e=document.RootElement.Clone();Check(e.GetProperty("nonce").GetString()==nonce&&e.GetProperty("pid").GetInt32()==process.Id,"Owned event");events.Add(e);}}
        JsonElement Stats(int expected){for(;;){Send("QueueStats");var value=Event("QUEUE_STATS");if(value.GetProperty("queued").GetInt32()==expected)return value;Check(watch.Elapsed.TotalSeconds<4,"Held ingress4s");Thread.Sleep(1);}}
        try{
            var route=Event("ROUTE_READY");var front=IPEndPoint.Parse(route.GetProperty("frontend").GetString()!);client.SendTo(new byte[]{42},front);Event("SOURCE_CANDIDATE");Send("AllowSource",(IPEndPoint)client.LocalEndPoint!);Event("SOURCE_ALLOWED");Receive(backend);
            Send("Arm");Event("ARMED");Send("HoldQueue");Event("HELD");
            client.SendTo(new byte[]{11},front);var below=Stats(1);Check(below.GetProperty("queuedBytes").GetInt64()==1,"Below count/byte boundary");
            if(scenario=="off-pending"){
                var old=below.GetProperty("entries")[0];Check(old.GetProperty("Epoch").GetInt32()==1,"Actually queued impaired epoch1");Send("Off");Event("OFF");var off=Stats(1);Check(off.GetProperty("epoch").GetInt32()==2&&off.GetProperty("entries")[0].GetRawText()==old.GetRawText(),"Off retains actual epoch/due/receipt/target/hash");
                Check(!backend.Poll(0,SelectMode.SelectRead),"Held delayed bytes not forwarded early");Send("ReleaseQueue");Event("RELEASED");var delivered=Receive(backend);Check(delivered.SequenceEqual(new byte[]{11}),"Old target actual unchanged payload after Off");Event("OFF_DRAINED");Send("Stop");Check(process.WaitForExit(5000)&&process.ExitCode==0,"Drain actual zero exit");
            }else{
                client.SendTo(new byte[]{12},front);var exact=Stats(2);Check(exact.GetProperty("queuedBytes").GetInt64()==2,"Exact selected cap");client.SendTo(new byte[]{13},front);Check(process.WaitForExit(5000)&&process.ExitCode==1,"Actual +1 ingress rejection");
            }
            using var document=JsonDocument.Parse(File.ReadAllText(report));var final=document.RootElement;Check(final.GetProperty("controlMode").GetBoolean()&&final.GetProperty("limits").GetProperty("datagrams").GetInt64()==countLimit&&final.GetProperty("limits").GetProperty("bytes").GetInt64()==byteLimit,"Declared control provenance");
            if(scenario!="off-pending")Check(final.GetProperty("overflowDatagrams").GetInt64()==1&&final.GetProperty("overflowBytes").GetInt64()==1&&final.GetProperty("queued").GetInt32()==2&&final.GetProperty("queuedBytes").GetInt64()==2,"Actual below/exact/+1 preserved overflow counts");
        }finally{if(!process.HasExited){process.Kill();process.WaitForExit();}File.WriteAllText(Path.Combine(directory,"stderr.log"),stderr.GetAwaiter().GetResult());var remaining=process.StandardOutput.ReadToEnd();File.AppendAllText(Path.Combine(directory,"stdout.log"),remaining);Console.WriteLine("Queue control evidence "+scenario+" "+directory);}
    }
    private static Socket Open(){var s=new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp);s.Bind(new IPEndPoint(IPAddress.Loopback,0));s.ReceiveTimeout=5000;return s;}
    private static byte[] Receive(Socket socket){var b=new byte[65535];EndPoint remote=new IPEndPoint(IPAddress.Any,0);var n=socket.ReceiveFrom(b,ref remote);return b.AsSpan(0,n).ToArray();}
}
