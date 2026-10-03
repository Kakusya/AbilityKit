using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;

namespace AbilityKit.Game.Cooking.DatagramImpairmentRelay;

// Executed only by an explicitly authorized --self-check invocation. No game authority.
internal static class RelayControls
{
    internal static int Run(string? report)
    {
        var checks=new List<string>();
        void Check(bool pass,string name){if(!pass)throw new InvalidOperationException(name);checks.Add(name);}
        try{
            Check(QueueBudget.CanAdmit(65535,64L*1024*1024-1,1),"exact shared queue last byte/entry");
            Check(!QueueBudget.CanAdmit(65536,0,0)&&!QueueBudget.CanAdmit(0,64L*1024*1024,1),"queue count/byte overflow rejection");
            foreach(var name in new[]{"P0","P1","P2","P3","P4","P5","P6"}){
                var policy=Policy.For(name);var seed=Decisions.Seed(name,1,1,"c2s");
                Check(seed==Decisions.Seed(name,1,1,"c2s"),name+" deterministic seed");
                Check(seed!=Decisions.Seed(name,1,1,"s2c")&&seed!=Decisions.Seed(name,1,2,"c2s"),name+" direction/route separation");
                for(var i=1;i<=10000;i++){
                    var a=Decisions.For(policy,seed,i);var b=Decisions.For(policy,seed,i);
                    Check(a==b&&a.Delay>=Math.Max(0,policy.Delay-policy.Jitter)&&a.Delay<=policy.Delay+policy.Jitter,name+" deterministic bounded decision "+i);
                    if(policy.LossBasisPoints==0)Check(!a.Drop,name+" no intentional loss "+i);
                }
            }
            using var source=new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp);
            using var destination=new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp);
            source.Bind(new IPEndPoint(IPAddress.Loopback,0));destination.Bind(new IPEndPoint(IPAddress.Loopback,0));destination.ReceiveTimeout=5000;
            foreach(var size in new[]{0,1,1500,65507}){
                var payload=RandomNumberGenerator.GetBytes(size);source.SendTo(payload,destination.LocalEndPoint!);var buffer=new byte[65535];EndPoint remote=new IPEndPoint(IPAddress.Any,0);
                var count=destination.ReceiveFrom(buffer,ref remote);Check(count==size&&buffer.AsSpan(0,count).SequenceEqual(payload)&&remote.Equals(source.LocalEndPoint),"actual raw socket payload/source "+size);
            }
            ControlledRelayChecks.Run();
            ControlledQueueChecks.Run();
            var result=JsonSerializer.Serialize(new{passed=true,checks=checks.Count,relaySha=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(RelayControls).Assembly.Location))),mvid=typeof(RelayControls).Assembly.ManifestModule.ModuleVersionId,machine=Environment.MachineName,scope="Deterministic policy/default shared-cap boundaries; actual raw owner-loop pin/delay/off/retirement, nonce/sequence/route controls, reduced control-only count/byte below/exact/+1 and Off pending original epoch/due/target/hash. Not giant default-cap OS saturation.",pid=Environment.ProcessId});
            if(report is not null){var path=Path.GetFullPath(report);Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,result);}Console.WriteLine(result);return 0;
        }catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
