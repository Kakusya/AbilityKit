using System.Text.Json;
using System.Security.Cryptography;
using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.NetworkProcessMeasurement;

namespace AbilityKit.Game.Cooking.NetworkImpairmentMeasurement;

internal static class RetryConservation
{
    internal sealed record Proof(int AppendedIdleTicks,long StateVersionDelta,long LogicalTickDelta,long EventSequenceDelta,string BeforeCanonicalHash,string AfterCanonicalHash,string NormalizedAfterCanonicalHash);
    internal static Proof Verify(CookingRecipeCheckpoint before,CookingRecipeCheckpoint after)
    {
        void Need(bool pass,string reason){if(!pass)throw new InvalidOperationException("Cached retry conservation: "+reason);}
        Need(before.LevelScope==ProcessMeasurementFixture.Scope&&after.LevelScope==before.LevelScope,"same bound scope");
        Need(before.Supply is null&&after.Supply is null,"fixture has no scheduled Supply to normalize");
        Need(before.Processes.Count==0&&after.Processes.Count==0,"no active process during idle proof");
        Need(after.TickEvents.Count>before.TickEvents.Count,"post-retry image has actual new owner tick");
        Need(JsonSerializer.Serialize(before.TickEvents)==JsonSerializer.Serialize(after.TickEvents.Take(before.TickEvents.Count).ToArray()),"exact append-only full tick prefix");
        var logical=before.LogicalTick;var version=before.StateVersion;var sequence=before.EventSequence;var frame=before.TickEvents.LastOrDefault()?.HostFrameSequence??0;
        foreach(var tick in after.TickEvents.Skip(before.TickEvents.Count)){
            Need(tick.LevelScope==before.LevelScope&&tick.Processes.Count==0,"actual idle tick scope/no process effects");
            Need(tick.HostFrameSequence==checked(frame+1)&&tick.Sequence==checked(sequence+1)&&tick.BeforeLogicalTick==logical&&tick.AfterLogicalTick==checked(logical+1)&&tick.BeforeStateVersion==version&&tick.AfterStateVersion==checked(version+1),"consecutive exact frame/logical/state/event ledger");
            frame=tick.HostFrameSequence;logical=tick.AfterLogicalTick;version=tick.AfterStateVersion;sequence=tick.Sequence;
        }
        Need(after.LogicalTick==logical&&after.StateVersion==version&&after.EventSequence==sequence,"all counter deltas exactly explained by actual appended ticks");
        var normalized=after with{LogicalTick=before.LogicalTick,StateVersion=before.StateVersion,EventSequence=before.EventSequence,TickEvents=before.TickEvents};
        // Serialize the entire current schema: no business field, schema/pose/container/allocator or future record property omitted.
        Need(JsonSerializer.Serialize(before)==JsonSerializer.Serialize(normalized),"entire checkpoint unchanged beyond proven idle clock fields");
        return new(after.TickEvents.Count-before.TickEvents.Count,after.StateVersion-before.StateVersion,after.LogicalTick-before.LogicalTick,after.EventSequence-before.EventSequence,before.Sha256(),after.Sha256(),normalized.Sha256());
    }
    internal static object Differences(CookingRecipeCheckpoint before,CookingRecipeCheckpoint after)
    {
        using var left=JsonDocument.Parse(JsonSerializer.Serialize(before));using var right=JsonDocument.Parse(JsonSerializer.Serialize(after));
        return left.RootElement.EnumerateObject().Where(p=>p.Value.GetRawText()!=right.RootElement.GetProperty(p.Name).GetRawText()).Select(p=>new{field=p.Name,before=p.Value.Clone(),after=right.RootElement.GetProperty(p.Name).Clone()}).ToArray();
    }
    internal static int Controls(string? report)
    {
        var checks=new List<string>();
        void Save(bool passed,object? proof,string? failure){var result=new{passed,checks,proof,failure,driverSha=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(RetryConservation).Assembly.Location))),driverMvid=typeof(RetryConservation).Assembly.ManifestModule.ModuleVersionId,machine=Environment.MachineName,stopwatchFrequency=System.Diagnostics.Stopwatch.Frequency,scope="Pure public simulation cached duplicate/idle ticks plus conservation classifier negatives, not a live network retry",pid=Environment.ProcessId};var json=JsonSerializer.Serialize(result);if(report is not null){var path=Path.GetFullPath(report);Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,json);}Console.WriteLine(json);}
        try{
            var fixture=new ProcessMeasurementFixture();var kitchen=fixture.Create(ProcessMeasurementFixture.Scope,fixture.Configuration);kitchen.AdvanceFixedTick(ProcessMeasurementFixture.Scope,1);
            CookingRecipeCommand Command(CookingRecipeOperation operation,string id,int batch){var item=kitchen.ExportCheckpoint().Items.Single(i=>i.Id==ProcessMeasurementFixture.Tools[1]);return new(ProcessMeasurementFixture.Match,batch,ProcessMeasurementFixture.Remote,new(id),operation,Item:item.Id,ExpectedItemVersion:item.Version,WorldAnchor:operation==CookingRecipeOperation.Drop?item.Id.Value:null);}
            var pickup=kitchen.ExecuteBatch(new[]{Command(CookingRecipeOperation.Pickup,"control-preflight-pickup",1)}).Single();if(pickup.Result.Outcome!=CookingRecipeOutcome.Accepted)throw new InvalidOperationException("Real preflight pickup");
            var original=Command(CookingRecipeOperation.Drop,"control-preflight-drop",2);var receipt=kitchen.ExecuteBatch(new[]{original}).Single();if(receipt.Result.Outcome!=CookingRecipeOutcome.Accepted)throw new InvalidOperationException("Real preflight drop");
            var before=kitchen.ExportCheckpoint();var replay=kitchen.ExecuteBatch(new[]{original}).Single();if(!replay.Result.IsDuplicate||replay.Result.StateVersion!=receipt.Result.StateVersion||replay.Result.Events.Count!=0)throw new InvalidOperationException("Real cached simulation receipt");
            kitchen.AdvanceFixedTick(ProcessMeasurementFixture.Scope,2);kitchen.AdvanceFixedTick(ProcessMeasurementFixture.Scope,3);var after=kitchen.ExportCheckpoint();
            var proof=Verify(before,after);if(proof.AppendedIdleTicks!=2)throw new InvalidOperationException("Real two-idle-tick proof");checks.Add("real public simulation idle frames account full checkpoint");
            if(after.Items.Count==0||after.Containers.Count==0||after.Poses is null||after.Poses.Count==0||after.TickEvents.Count<3)throw new InvalidOperationException("Negative-control fixture must populate items/containers/poses/tick ledger");
            void Reject(CookingRecipeCheckpoint changed,string reason){if(JsonSerializer.Serialize(changed)==JsonSerializer.Serialize(after))throw new InvalidOperationException("Degenerate unchanged negative input: "+reason);try{Verify(before,changed);}catch(InvalidOperationException){checks.Add(reason);return;}throw new InvalidOperationException("Mutation incorrectly conserved: "+reason);}
            Reject(after with{NextProductId=after.NextProductId+1},"allocator mutation");
            Reject(after with{Items=after.Items.Select((i,n)=>n==0?i with{Version=i.Version+1}:i).ToArray()},"item mutation");
            Reject(after with{Containers=after.Containers.Select((c,n)=>n==0?c with{ItemIds=new[]{ProcessMeasurementFixture.Input}}:c).ToArray()},"container content mutation");
            Reject(after with{SchemaVersion=after.SchemaVersion+1},"schema mutation");
            Reject(after with{Poses=after.Poses!.Select((p,n)=>n==0?p with{X=p.X+1}:p).ToArray()},"pose mutation");
            Reject(after with{TickEvents=after.TickEvents.Select((t,n)=>n==after.TickEvents.Count-1?t with{HostFrameSequence=t.HostFrameSequence+1}:t).ToArray()},"forged/discontinuous frame ledger");
            Reject(after with{StateVersion=after.StateVersion+1},"unaccounted state version");
            var actualMutation=new CookingRecipeCommand(ProcessMeasurementFixture.Match,1,ProcessMeasurementFixture.Remote,new("control-fresh-pickup"),CookingRecipeOperation.Pickup,Item:ProcessMeasurementFixture.Tools[1],ExpectedItemVersion:after.Items.Single(i=>i.Id==ProcessMeasurementFixture.Tools[1]).Version);
            var execution=kitchen.ExecuteBatch(new[]{actualMutation}).Single();if(execution.Result.Outcome!=CookingRecipeOutcome.Accepted)throw new InvalidOperationException("Real negative-control command must be accepted");kitchen.AdvanceFixedTick(ProcessMeasurementFixture.Scope,4);var changed=kitchen.ExportCheckpoint();Reject(changed,"real fresh command/receipt is not idle duplicate conservation");
            Reject(after with{Deduplication=changed.Deduplication},"receipt-only mutation");
            Save(true,proof,null);return 0;
        }catch(Exception error){Save(false,null,error.ToString());Console.Error.WriteLine(error);return 1;}
    }
}
