using AbilityKit.Game.Cooking;
using AbilityKit.Game.Cooking.EtRuntime;
using Xunit;

namespace AbilityKit.ET.Runtime.Tests;

[Trait("Gate", "CookingLevelRuntime")]
public sealed class CookingCoreEtExpansionTests
{
    private static readonly PlayerId A = new("a"), B = new("b");
    private static readonly ItemId Pot = new("pot"), Bowl = new("bowl"), Input = new("input");
    private static readonly DefinitionId Raw = new("raw"), Product = new("product"), PotDef = new("pot-def"), BowlDef = new("bowl-def");
    private static readonly StationSlotId Board = new("board"), Counter = new("counter");
    private static readonly RecipeId Recipe = new("recipe");
    private static readonly CookingScope Scope = new(new("core-session"),new("world"),new("match"));
    private static readonly CookingLevelScope Level = new(Scope,new(1),new("level"),1);

    private sealed class Factory : ICookingLevelGameplayFactory
    {
        public CookingRecipeFixture Fixture { get; }
        public CookingConfigurationSnapshot Config { get; }
        public CookingRecipeSimulation Simulation { get; private set; } = null!;
        public Factory(int speed=1000, bool wall=false, bool manual=true)
        {
            var items = new[]{new CookingItemDefinition(Raw,new HashSet<string>{"cook"}),new CookingItemDefinition(Product,new HashSet<string>{"cook"}),
                new CookingItemDefinition(PotDef,new HashSet<string>{"cook"},new(3,new HashSet<DefinitionId>{Raw,Product})),
                new CookingItemDefinition(BowlDef,new HashSet<string>{"cook"},new(4,new HashSet<DefinitionId>{Product}))};
            var stations = new[]{new CookingApplianceDefinition(Board,new HashSet<string>{"work"}),new CookingApplianceDefinition(Counter,new HashSet<string>())};
            var recipes = new[]{new CookingRecipeDefinition(Recipe,new[]{Raw},Product,new("work"),"work",4,
                Completion:CookingRecipeCompletionKind.RetainInputs,Execution:manual?CookingRecipeExecutionKind.Manual:CookingRecipeExecutionKind.Automatic,YieldPortions:3)};
            var spatial = new CookingSpatialConfiguration(-5000,-5000,5000,5000,50,1000,
                new[]{new CookingPlayerPose(A,0,0,1,0),new CookingPlayerPose(B,0,300,1,0)},
                new[]{new CookingSpatialAnchor(LocationKind.StationSlot,"board",500,0),new CookingSpatialAnchor(LocationKind.StationSlot,"counter",500,300),
                    new CookingSpatialAnchor(LocationKind.WorldPosition,"empty",400,200)},
                wall?new[]{new CookingSpatialObstacle(200,-100,250,100)}:Array.Empty<CookingSpatialObstacle>(),speed);
            var registry = new CookingConfigurationRegistry();
            Assert.True(registry.Submit(new(new[]{"work"},items,stations,recipes,Spatial:spatial)).Accepted);
            Config=registry.Current!;
            Fixture=new(Scope,new Dictionary<PlayerId,CookingPlayerConfig>{[A]=new(A,new HashSet<string>{"cook"},new HashSet<string>{"board","counter"}),
                [B]=new(B,new HashSet<string>{"cook"},new HashSet<string>{"board","counter"})},items.ToDictionary(i=>i.Id),stations.ToDictionary(i=>i.Station),recipes.ToDictionary(i=>i.Id),spatial:spatial);
        }
        public CookingRecipeSimulation Create(CookingLevelScope scope,CookingConfigurationSnapshot config)
        {
            Simulation=new(Fixture);Simulation.AddItem(Pot,PotDef,ItemLocation.Station(Board));Simulation.AddItem(Bowl,BowlDef,ItemLocation.Station(Counter));
            Simulation.AddItem(Input,Raw,ItemLocation.Container(Pot,"slot-0"));return Simulation;
        }
        public CookingLevelEtHost Start()
        {
            var host=new CookingLevelEtHost(new CookingLevelLifecycle(Level,Config,this));
            Assert.True(host.Prepare(new(Level.Level,new("map"),new(new("layout"),new[]{Board,Counter},new[]{PotDef,BowlDef}),Config.Identity)).Accepted);
            Assert.True(host.Start().Accepted);return host;
        }
    }
    private static CookingRecipeCommand Cmd(Factory f,CookingLevelEtHost h,CookingRecipeOperation op,string id,PlayerId? player=null,ItemId? item=null,ItemId? container=null,ProcessId? process=null,
        StationSlotId? station=null,int mx=0,int my=0,int fx=0,int fy=0,string? world=null) =>
        new(Scope,h.HostFrameSequence+1,player??A,new(id),op,Item:item,Container:container,Process:process,Station:station,
            ExpectedItemVersion:item is {} i?f.Simulation.Snapshot().Items.Single(x=>x.Id==i).Version:0,MoveX:mx,MoveY:my,FacingX:fx,FacingY:fy,WorldAnchor:world);
    private static CookingRecipeCommandResult Execute(CookingLevelEtHost host,CookingRecipeCommand command)
    {
        Assert.True(host.TryEnqueue(new(Level,command,"connection",command.Command.Value)).Accepted);
        var frame=host.Tick();Assert.True(frame.Accepted);
        return Assert.Single(frame.Dispositions).Result!;
    }
    private static void Accepted(CookingRecipeCommandResult result)=>Assert.Equal(CookingRecipeOutcome.Accepted,result.Outcome);

    [Fact] public void Manual_pause_swap_portions_survive_real_ingress_checkpoint_destroy_restore_continue()
    {
        var f=new Factory();CookingLevelCheckpoint checkpoint;string expected;
        using(var host=f.Start())
        {
            Accepted(Execute(host,Cmd(f,host,CookingRecipeOperation.StartProcess,"start",item:Pot,station:Board)));
            host.Tick();var process=Assert.Single(f.Simulation.Snapshot().Processes).Id;
            Assert.Equal(2,Assert.Single(f.Simulation.Snapshot().Processes).ElapsedTicks);
            Accepted(Execute(host,Cmd(f,host,CookingRecipeOperation.StopProcess,"stop",process:process)));
            for(var i=0;i<5;i++)host.Tick();Assert.Equal(2,Assert.Single(f.Simulation.Snapshot().Processes).ElapsedTicks);
            checkpoint=host.ExportCheckpoint().Checkpoint!;
            var serialized=CookingLevelCheckpointCodec.Serialize(CookingLevelCheckpointCodec.CreateEnvelope(checkpoint));
            var decoded=CookingLevelCheckpointCodec.Deserialize(serialized);Assert.True(decoded.Accepted);checkpoint=decoded.Checkpoint!;
            Assert.False(CookingLevelCheckpointCodec.Deserialize(serialized.Replace("\"formatVersion\":7","\"formatVersion\":5")).Accepted);
            Assert.False(CookingLevelCheckpointCodec.Deserialize(serialized.Replace("\"remainingPortions\":0", "\"removedPortions\":0")).Accepted);
            Finish(f,host,process);expected=f.Simulation.Snapshot().CanonicalText();
        }
        var restoredFactory=new Factory();var restored=CookingLevelEtHost.Restore(checkpoint,restoredFactory.Config,restoredFactory);Assert.True(restored.Accepted);
        using(var host=restored.Host!)
        {
            Finish(restoredFactory,host,Assert.Single(restoredFactory.Simulation.Snapshot().Processes).Id);
            Assert.Equal(expected,restoredFactory.Simulation.Snapshot().CanonicalText());
            Assert.Empty(restoredFactory.Simulation.ItemsInContainer(Pot));
            Assert.Equal(3,restoredFactory.Simulation.ItemsInContainer(Bowl).Count);
        }
    }
    private static void Finish(Factory f,CookingLevelEtHost host,ProcessId process)
    {
        Accepted(Execute(host,Cmd(f,host,CookingRecipeOperation.ContinueProcess,"continue",B,process:process)));host.Tick();
        Assert.Empty(f.Simulation.Snapshot().Processes);
        Accepted(Execute(host,Cmd(f,host,CookingRecipeOperation.ServePortion,"one",item:Pot,container:Bowl)));
        var partial=host.ExportCheckpoint().Checkpoint!;Assert.Equal(2,partial.Recipe.Items.Single(i=>i.Id==Pot).RemainingPortions);
        Accepted(Execute(host,Cmd(f,host,CookingRecipeOperation.ServePortion,"two",item:Pot,container:Bowl)));
        var a=Cmd(f,host,CookingRecipeOperation.ServePortion,"last",item:Pot,container:Bowl);var b=a with{Player=B};
        Assert.True(host.TryEnqueue(new(Level,b,"b","b")).Accepted);Assert.True(host.TryEnqueue(new(Level,a,"a","a")).Accepted);
        var frame=host.Tick();Assert.Single(frame.Dispositions,d=>d.Result?.Outcome==CookingRecipeOutcome.Accepted);
    }
    [Fact] public void Direction_normalization_stop_speed_same_tick_cap_and_world_table_ingress()
    {
        var f=new Factory();using(var host=f.Start())
        {
            Accepted(Execute(host,Cmd(f,host,CookingRecipeOperation.Move,"diagonal",mx:-1000,my:-1000)));
            var pose=f.Simulation.Snapshot().Poses!.Single(p=>p.Player==A);Assert.Equal(-707,pose.X);Assert.Equal(-707,pose.Y);Assert.Equal(1,pose.FacingX);
            Accepted(Execute(host,Cmd(f,host,CookingRecipeOperation.Move,"stop")));Assert.Equal(pose.X,f.Simulation.Snapshot().Poses!.Single(p=>p.Player==A).X);
        }
        f=new Factory(speed:150);using(var host=f.Start())
        {
            var first=Cmd(f,host,CookingRecipeOperation.Move,"a",my:-1000);var second=first with{Command=new("b")};
            Assert.True(host.TryEnqueue(new(Level,second,"b","b")).Accepted);Assert.True(host.TryEnqueue(new(Level,first,"a","a")).Accepted);
            Assert.Single(host.Tick().Dispositions,d=>d.Result?.Outcome==CookingRecipeOutcome.Accepted);
            Assert.Equal(-150,f.Simulation.Snapshot().Poses!.Single(p=>p.Player==A).Y);
            Accepted(Execute(host,Cmd(f,host,CookingRecipeOperation.Pickup,"pickup",item:Pot)));
            Accepted(Execute(host,Cmd(f,host,CookingRecipeOperation.Drop,"world",item:Pot,world:"empty")));
            Assert.Equal(ItemLocation.World("empty"),f.Simulation.Snapshot().Items.Single(i=>i.Id==Pot).Location);
        }
    }
    [Fact] public void Wall_sliding_and_fingerprint_new_fields_are_authoritative()
    {
        var f=new Factory(wall:true);using var host=f.Start();
        Assert.Equal(CookingRecipeRejectionReason.TargetOutOfRange,Execute(host,Cmd(f,host,CookingRecipeOperation.Pickup,"wall-pickup",item:Pot)).Reason);
        Accepted(Execute(host,Cmd(f,host,CookingRecipeOperation.Move,"slide",mx:1000,my:-1000)));
        var pose=f.Simulation.Snapshot().Poses!.Single(p=>p.Player==A);Assert.Equal(0,pose.X);Assert.Equal(-707,pose.Y);
        var c=Cmd(f,host,CookingRecipeOperation.Move,"finger",mx:-1000);
        var e=new CookingLevelCommandEnvelope(Level,c,"c","c");var bytes=CookingCommandFingerprint.Create(e);
        Assert.NotEqual(bytes,CookingCommandFingerprint.Create(e with{Command=c with{MoveY=1}}));
        Assert.NotEqual(bytes,CookingCommandFingerprint.Create(e with{Command=c with{FacingY=1}}));
        var d=Cmd(f,host,CookingRecipeOperation.Drop,"drop",item:Pot,world:"empty");
        Assert.NotEqual(CookingCommandFingerprint.Create(e with{Command=d}),CookingCommandFingerprint.Create(e with{Command=d with{WorldAnchor="other"}}));
    }
    [Fact] public void Active_worker_pose_and_partial_balance_restore_reconstruct_and_reject_tampering()
    {
        var f=new Factory(speed:150);CookingLevelCheckpoint active,partial;string expected;
        using(var h=f.Start())
        {
            Accepted(Execute(h,Cmd(f,h,CookingRecipeOperation.Move,"step",my:-1000)));
            Accepted(Execute(h,Cmd(f,h,CookingRecipeOperation.StartProcess,"work",item:Pot,station:Board)));h.Tick();
            active=h.ExportCheckpoint().Checkpoint!;Assert.Equal(A,Assert.Single(active.Recipe.Processes).ActiveWorker);
            h.Tick();h.Tick();Accepted(Execute(h,Cmd(f,h,CookingRecipeOperation.ServePortion,"p1",item:Pot,container:Bowl)));
            partial=h.ExportCheckpoint().Checkpoint!;
            Accepted(Execute(h,Cmd(f,h,CookingRecipeOperation.ServePortion,"p2",item:Pot,container:Bowl)));
            Accepted(Execute(h,Cmd(f,h,CookingRecipeOperation.ServePortion,"p3",item:Pot,container:Bowl)));expected=f.Simulation.Snapshot().CanonicalText();
        }
        var bad=active with{Recipe=active.Recipe with{Poses=active.Recipe.Poses!.Select(p=>p with{X=10000}).ToArray()}};
        var badFactory=new Factory(speed:150);Assert.False(CookingLevelEtHost.Restore(bad,badFactory.Config,badFactory).Accepted);
        bad=active with{Recipe=active.Recipe with{Processes=active.Recipe.Processes.Select(p=>p with{ActiveWorker=new PlayerId("ghost")}).ToArray()}};
        Assert.False(CookingLevelEtHost.Restore(bad,badFactory.Config,badFactory).Accepted);
        var rebuilt=new Factory(speed:150);var result=CookingLevelEtHost.Restore(active,rebuilt.Config,rebuilt);Assert.True(result.Accepted);
        using(var h=result.Host!)
        {
            Assert.Equal(active.Recipe.Poses,rebuilt.Simulation.ExportCheckpoint().Poses);
            h.Tick();h.Tick();Accepted(Execute(h,Cmd(rebuilt,h,CookingRecipeOperation.ServePortion,"p1",item:Pot,container:Bowl)));
            Assert.Equal(partial.Recipe.CanonicalText(),rebuilt.Simulation.ExportCheckpoint().CanonicalText());
        }
        rebuilt=new Factory(speed:150);result=CookingLevelEtHost.Restore(partial,rebuilt.Config,rebuilt);Assert.True(result.Accepted);
        using(var h=result.Host!)
        {
            Accepted(Execute(h,Cmd(rebuilt,h,CookingRecipeOperation.ServePortion,"p2",item:Pot,container:Bowl)));
            Accepted(Execute(h,Cmd(rebuilt,h,CookingRecipeOperation.ServePortion,"p3",item:Pot,container:Bowl)));
            Assert.Equal(expected,rebuilt.Simulation.Snapshot().CanonicalText());
        }
    }
    [Fact] public void Geometry_preview_race_and_stable_player_collision_execute_via_ingress()
    {
        var f=new Factory(speed:150);using var host=f.Start();
        var before=f.Simulation.ExportCheckpoint().CanonicalText();
        var preview=Assert.Single(f.Simulation.PreviewInteraction(B,1,new("race")),p=>p.TargetId==Pot.Value);
        Assert.Equal(before,f.Simulation.ExportCheckpoint().CanonicalText());
        Accepted(Execute(host,Cmd(f,host,CookingRecipeOperation.Pickup,"first",item:Pot)));
        Assert.Equal(CookingRecipeRejectionReason.TargetOutOfRange,Execute(host,preview.Command with{SimulationBatch=host.HostFrameSequence+1}).Reason);
        var a=Cmd(f,host,CookingRecipeOperation.Move,"move",my:1000);var b=a with{Player=B,MoveY=-1000};
        Assert.True(host.TryEnqueue(new(Level,b,"b","b")).Accepted);Assert.True(host.TryEnqueue(new(Level,a,"a","a")).Accepted);
        var frame=host.Tick();Assert.Single(frame.Dispositions,d=>d.Result?.Outcome==CookingRecipeOutcome.Accepted);
        Assert.Equal(150,f.Simulation.Snapshot().Poses!.Single(p=>p.Player==A).Y);
        Assert.Equal(300,f.Simulation.Snapshot().Poses!.Single(p=>p.Player==B).Y);
    }

    [Fact] public void Same_tick_stop_swap_and_dual_claim_arbitrate_without_extra_manual_progress()
    {
        var f=new Factory();using var h=f.Start();
        Accepted(Execute(h,Cmd(f,h,CookingRecipeOperation.StartProcess,"start",item:Pot,station:Board)));
        var process=Assert.Single(f.Simulation.Snapshot().Processes).Id;
        var stop=Cmd(f,h,CookingRecipeOperation.StopProcess,"stop",process:process);
        var take=Cmd(f,h,CookingRecipeOperation.ContinueProcess,"take",B,process:process);
        Assert.True(h.TryEnqueue(new(Level,take,"b","b")).Accepted);Assert.True(h.TryEnqueue(new(Level,stop,"a","a")).Accepted);
        Assert.All(h.Tick().Dispositions,d=>Assert.Equal(CookingRecipeOutcome.Accepted,d.Result!.Outcome));
        var state=Assert.Single(f.Simulation.Snapshot().Processes);Assert.Equal(B,state.ActiveWorker);Assert.Equal(2,state.ElapsedTicks);
        Accepted(Execute(h,Cmd(f,h,CookingRecipeOperation.StopProcess,"stop-b",B,process:process)));
        var a=Cmd(f,h,CookingRecipeOperation.ContinueProcess,"claim",process:process);var b=a with{Player=B};
        Assert.True(h.TryEnqueue(new(Level,b,"b","b")).Accepted);Assert.True(h.TryEnqueue(new(Level,a,"a","a")).Accepted);
        Assert.Single(h.Tick().Dispositions,d=>d.Result?.Outcome==CookingRecipeOutcome.Accepted);
        state=Assert.Single(f.Simulation.Snapshot().Processes);Assert.Equal(A,state.ActiveWorker);Assert.Equal(3,state.ElapsedTicks);
    }

}
