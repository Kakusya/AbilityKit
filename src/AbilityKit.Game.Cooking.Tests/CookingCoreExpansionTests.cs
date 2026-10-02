using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingCoreExpansionTests
{
    private static readonly PlayerId A = new("a"), B = new("b");
    private static readonly ItemId Pot = new("pot"), Bowl = new("bowl"), RawItem = new("raw-item");
    private static readonly DefinitionId Raw = new("raw"), Product = new("product"), PotDef = new("pot-def"), BowlDef = new("bowl-def");
    private static readonly StationSlotId Board = new("board"), Counter = new("counter"), Far = new("far");
    private static readonly RecipeId Recipe = new("recipe");
    private static readonly CookingScope Scope = new(new("s"), new("w"), new("m"));
    private static readonly CookingLevelScope Level = new(Scope, new(1), new("level"), 1);

    private static CookingRecipeFixture Fixture(bool manual = false, int portions = 1, bool spatial = true, bool wall = false, int capacity = 4,
        CookingRecipeCompletionKind completion = CookingRecipeCompletionKind.RetainInputs)
    {
        var players = new Dictionary<PlayerId, CookingPlayerConfig>
        {
            [A] = new(A, new HashSet<string>{"cook"}, new HashSet<string>{"board","counter"}),
            [B] = new(B, new HashSet<string>{"cook"}, new HashSet<string>{"board","counter"}),
        };
        var items = new Dictionary<DefinitionId, CookingItemDefinition>
        {
            [Raw] = new(Raw, new HashSet<string>{"cook"}), [Product] = new(Product, new HashSet<string>{"cook"}),
            [PotDef] = new(PotDef, new HashSet<string>{"cook"}, new(4,new HashSet<DefinitionId>{Raw,Product})),
            [BowlDef] = new(BowlDef, new HashSet<string>{"cook"}, new(capacity,new HashSet<DefinitionId>{Product})),
        };
        var appliances = new Dictionary<StationSlotId,CookingApplianceDefinition>
        {
            [Board] = new(Board,new HashSet<string>{"work"}), [Counter] = new(Counter,new HashSet<string>()),
            [Far] = new(Far,new HashSet<string>()),
        };
        var geometry = new CookingSpatialConfiguration(-5000,-5000,5000,5000,50,1000,
            new[]{new CookingPlayerPose(A,0,0,1,0),new CookingPlayerPose(B,0,300,1,0)},
            new[]{new CookingSpatialAnchor(LocationKind.StationSlot,"board",500,0),
                new CookingSpatialAnchor(LocationKind.StationSlot,"counter",500,300),
                new CookingSpatialAnchor(LocationKind.StationSlot,"far",3000,0),
                new CookingSpatialAnchor(LocationKind.WorldPosition,"spawn",500,0),
                new CookingSpatialAnchor(LocationKind.WorldPosition,"clean-pool",500,300)},
            wall ? new[]{new CookingSpatialObstacle(200,-100,250,100)} : Array.Empty<CookingSpatialObstacle>());
        return new(Scope,players,items,appliances,
            new Dictionary<RecipeId,CookingRecipeDefinition>{[Recipe]=new(Recipe,new[]{Raw},Product,new("work"),"work",4,
                Completion:completion,Execution:manual ? CookingRecipeExecutionKind.Manual:CookingRecipeExecutionKind.Automatic,YieldPortions:portions)},
            spatial:spatial ? geometry:null);
    }
    private static CookingRecipeSimulation Sim(bool manual=false,int portions=1,bool spatial=true,bool wall=false,int capacity=4,
        ICookingProductIdAllocator? allocator=null)
    {
        var s = new CookingRecipeSimulation(Fixture(manual,portions,spatial,wall,capacity),allocator);
        s.AddItem(Pot,PotDef,ItemLocation.Station(Board));
        s.AddItem(Bowl,BowlDef,ItemLocation.Station(Counter));
        s.AddItem(RawItem,Raw,ItemLocation.Container(Pot,"slot-0"));
        return s;
    }
    private static CookingRecipeCommand Cmd(CookingRecipeSimulation s,CookingRecipeOperation op,string id,PlayerId? player=null,
        ItemId? item=null,ItemId? container=null,StationSlotId? station=null,ProcessId? process=null) =>
        new(Scope,1,player??A,new(id),op,Item:item,Container:container,Station:station,Process:process,
            ExpectedItemVersion:item is {} i?s.Snapshot().Items.Single(x=>x.Id==i).Version:0);
    private static void Accept(CookingRecipeCommandResult r) => Assert.True(r.Outcome == CookingRecipeOutcome.Accepted, r.Reason.ToString());
    private static ProcessId Start(CookingRecipeSimulation s)
    {
        Accept(s.Submit(Cmd(s,CookingRecipeOperation.StartProcess,"start",item:Pot,station:Board)));
        return Assert.Single(s.Snapshot().Processes).Id;
    }
    private static void Tick(CookingRecipeSimulation s,int count)
    {
        for(var i=0;i<count;i++) s.AdvanceFixedTick(Level,s.LogicalTick+1);
    }
    private static void Complete(CookingRecipeSimulation s) {Start(s);Tick(s,4);}

    [Fact] public void Geometry_turn_move_wall_and_world_anchor_change_reach_and_preview_is_read_only()
    {
        var s=Sim();s.AddWorldIngredient(new("world-item"),Raw,"spawn");
        var before=s.ExportCheckpoint().CanonicalText();
        var preview=s.PreviewInteraction(A,1,new("preview"));
        Assert.Contains(preview,p=>p.Command.Operation==CookingRecipeOperation.Pickup && p.TargetId=="world-item");
        Assert.Equal(before,s.ExportCheckpoint().CanonicalText());
        Accept(s.Submit(new(Scope,1,A,new("turn"),CookingRecipeOperation.Move,FacingX:-1)));
        var hash=s.Snapshot().Sha256();
        Assert.Equal(CookingRecipeRejectionReason.TargetOutOfRange,s.Submit(Cmd(s,CookingRecipeOperation.Pickup,"back",item:new("world-item"))).Reason);
        Assert.Equal(hash,s.Snapshot().Sha256());
        Accept(s.Submit(new(Scope,1,A,new("walk"),CookingRecipeOperation.Move,MoveX:-1000,FacingX:1)));
        Assert.False(s.ValidateSpatialReach(A,LocationKind.WorldPosition,"spawn"));
        var w=Sim(wall:true);Assert.False(w.ValidateSpatialReach(A,LocationKind.WorldPosition,"spawn"));
        Assert.Equal(CookingRecipeRejectionReason.MovementBlocked,w.Submit(new(Scope,1,A,new("sweep"),CookingRecipeOperation.Move,MoveX:500,FacingX:1)).Reason);
        Assert.Throws<ArgumentException>(()=>s.AddWorldIngredient(new("unknown"),Raw,"missing"));
    }

    [Fact] public void Stable_batch_movement_collision_and_preview_race_revalidate()
    {
        var f=Fixture();
        var s=new CookingRecipeSimulation(new CookingRecipeFixture(f.Scope,f.Players,f.Items,f.Appliances,f.Recipes,spatial:f.Spatial! with{MovementSpeed=150}));
        var moveA=new CookingRecipeCommand(Scope,1,A,new("move"),CookingRecipeOperation.Move,MoveY:150,FacingX:1);
        var moveB=moveA with{Player=B,MoveY=-150};
        var results=s.SubmitBatch(new[]{moveB,moveA});
        Accept(results[0]);Assert.Equal(CookingRecipeRejectionReason.MovementBlocked,results[1].Reason);
        var other=new CookingRecipeSimulation(new CookingRecipeFixture(f.Scope,f.Players,f.Items,f.Appliances,f.Recipes,spatial:f.Spatial! with{MovementSpeed=150}));other.SubmitBatch(new[]{moveA,moveB});Assert.Equal(s.Snapshot().CanonicalText(),other.Snapshot().CanonicalText());
        var race=Sim();
        var candidate=Assert.Single(race.PreviewInteraction(B,1,new("race")),p=>p.Command.Operation==CookingRecipeOperation.Pickup && p.TargetId==Pot.Value);
        Accept(race.Submit(Cmd(race,CookingRecipeOperation.Pickup,"first",item:Pot)));
        Assert.Equal(CookingRecipeRejectionReason.TargetOutOfRange,race.Submit(candidate.Command).Reason);
    }

    [Theory]
    [InlineData(CookingRecipeOperation.Pickup)] [InlineData(CookingRecipeOperation.Drop)]
    [InlineData(CookingRecipeOperation.StartProcess)] [InlineData(CookingRecipeOperation.PutIn)]
    [InlineData(CookingRecipeOperation.TakeOut)] [InlineData(CookingRecipeOperation.Pour)]
    [InlineData(CookingRecipeOperation.SubmitOrder)] [InlineData(CookingRecipeOperation.ServePortion)]
    [InlineData(CookingRecipeOperation.ClearContents)] [InlineData(CookingRecipeOperation.DiscardItem)]
    public void Every_interaction_rechecks_geometry(CookingRecipeOperation operation)
    {
        var s=Sim();Accept(s.Submit(new(Scope,1,A,new("leave"),CookingRecipeOperation.Move,MoveX:-1000,FacingX:1)));
        var command=Cmd(s,operation,"far",item:operation==CookingRecipeOperation.TakeOut?RawItem:Pot,
            container:operation is CookingRecipeOperation.PutIn or CookingRecipeOperation.TakeOut ?Pot:Bowl,
            station:Board) with{Order=new OrderId("order")};
        var before=s.Snapshot().CanonicalText();
        Assert.Equal(CookingRecipeRejectionReason.TargetOutOfRange,s.Submit(command).Reason);
        Assert.Equal(before,s.Snapshot().CanonicalText());
    }

    [Fact] public void Manual_two_ticks_stop_five_ticks_other_worker_finishes_without_acceleration()
    {
        var s=Sim(manual:true);var process=Start(s);Tick(s,2);
        Assert.Equal(2,Assert.Single(s.Snapshot().Processes).ElapsedTicks);
        Assert.Equal(CookingRecipeRejectionReason.WorkerUnavailable,s.Submit(new(Scope,1,A,new("legacy"),CookingRecipeOperation.AdvanceTicks,Process:process,TickCount:10)).Reason);
        Accept(s.Submit(Cmd(s,CookingRecipeOperation.StopProcess,"stop",process:process)));Tick(s,5);
        Assert.Equal(2,Assert.Single(s.Snapshot().Processes).ElapsedTicks);
        Assert.Equal(CookingRecipeRejectionReason.ItemStale,s.Submit(Cmd(s,CookingRecipeOperation.ClearContents,"locked",item:Pot)).Reason);
        Accept(s.Submit(Cmd(s,CookingRecipeOperation.ContinueProcess,"continue",B,process:process)));
        Assert.Equal(CookingRecipeRejectionReason.WorkerUnavailable,s.Submit(Cmd(s,CookingRecipeOperation.ContinueProcess,"double",process:process)).Reason);
        Tick(s,1);Assert.Equal(3,Assert.Single(s.Snapshot().Processes).ElapsedTicks);Tick(s,1);Assert.Empty(s.Snapshot().Processes);
    }

    [Fact] public void Leaving_releases_manual_worker_and_automatic_device_continues()
    {
        var manual=Sim(manual:true);Start(manual);Tick(manual,1);
        Accept(manual.Submit(new(Scope,1,A,new("leave"),CookingRecipeOperation.Move,MoveX:-1000,FacingX:1)));Tick(manual,5);
        Assert.Null(Assert.Single(manual.Snapshot().Processes).ActiveWorker);Assert.Equal(1,Assert.Single(manual.Snapshot().Processes).ElapsedTicks);
        var auto=Sim();Start(auto);Accept(auto.Submit(Cmd(auto,CookingRecipeOperation.Pickup,"carry",item:Pot)));
        Accept(auto.Submit(new(Scope,1,A,new("walk"),CookingRecipeOperation.Move,MoveX:-1000,FacingX:1)));Tick(auto,4);
        Assert.True(auto.Snapshot().Items.Single(i=>i.Id==Pot).ContainerCompleted);
    }

    [Fact] public void Three_portions_conserve_inputs_last_portion_race_and_duplicate_conflict()
    {
        var s=Sim(portions:3);Complete(s);
        var first=Cmd(s,CookingRecipeOperation.ServePortion,"one",item:Pot,container:Bowl);Accept(s.Submit(first));
        Assert.True(s.Submit(first).IsDuplicate);Assert.Equal(CookingRecipeRejectionReason.CommandIdentityConflict,s.Submit(first with{Container=Pot}).Reason);
        Assert.Single(s.ItemsInContainer(Pot));Assert.Equal(2,s.Snapshot().Items.Single(i=>i.Id==Pot).RemainingPortions);
        Accept(s.Submit(Cmd(s,CookingRecipeOperation.ServePortion,"two",item:Pot,container:Bowl)));
        var a=Cmd(s,CookingRecipeOperation.ServePortion,"last",item:Pot,container:Bowl);
        var b=a with{Player=B};var results=s.SubmitBatch(new[]{b,a});
        Assert.Single(results,r=>r.Outcome==CookingRecipeOutcome.Accepted);Assert.Equal(3,s.Snapshot().Items.Count(i=>i.IsProduct));
        Assert.Empty(s.ItemsInContainer(Pot));Assert.Equal(0,s.Snapshot().Items.Single(i=>i.Id==Pot).RemainingPortions);
        Assert.Equal(0,s.CalculateTotalScore());
    }

    [Fact] public void Whole_batch_pour_preflights_capacity_and_clear_reuses_container()
    {
        var s=Sim(portions:3,capacity:2);Complete(s);var before=s.ExportCheckpoint();
        Assert.Equal(CookingRecipeRejectionReason.BatchCompleted,s.Submit(Cmd(s,CookingRecipeOperation.Pour,"full",item:Pot,container:Bowl)).Reason);
        Assert.Equal(before.NextProductId,s.ExportCheckpoint().NextProductId);Assert.Equal(before.Items,s.ExportCheckpoint().Items);
        Assert.Equal(CookingRecipeRejectionReason.BatchCompleted,s.Submit(Cmd(s,CookingRecipeOperation.TakeOut,"edit",item:RawItem,container:Pot)).Reason);
        Accept(s.Submit(Cmd(s,CookingRecipeOperation.ClearContents,"clear",item:Pot)));Assert.Empty(s.ItemsInContainer(Pot));
        var container=s.Snapshot().Items.Single(i=>i.Id==Pot);Assert.Null(container.Recipe);Assert.False(container.ContainerCompleted);
        Assert.Equal(CookingRecipeRejectionReason.ContainerRejectsItem,s.Submit(Cmd(s,CookingRecipeOperation.DiscardItem,"device",item:Pot)).Reason);
        s.AddItem(new("new-input"),Raw,ItemLocation.Hand(A));Accept(s.Submit(Cmd(s,CookingRecipeOperation.PutIn,"refill",item:new("new-input"),container:Pot)));
        var all=Sim(portions:3);Complete(all);Assert.Equal(CookingRecipeRejectionReason.BatchCompleted,all.Submit(Cmd(all,CookingRecipeOperation.Pour,"all",item:Pot,container:Bowl)).Reason);
        Assert.Empty(all.ItemsInContainer(Bowl));Assert.Single(all.ItemsInContainer(Pot));
    }

    [Theory] [InlineData("")] [InlineData("pot")]
    public void Allocator_failure_and_overflow_leave_checkpoint_including_counter_unchanged(string invalidIdentity)
    {
        var s=Sim(portions:3,allocator:new BrokenAllocator(invalidIdentity));Complete(s);var before=s.ExportCheckpoint().CanonicalText();
        Assert.Throws<InvalidOperationException>(()=>s.Submit(Cmd(s,CookingRecipeOperation.ServePortion,"bad",item:Pot,container:Bowl)));
        Assert.Equal(before,s.ExportCheckpoint().CanonicalText());
        var normal=Sim(portions:3);Complete(normal);var c=normal.ExportCheckpoint() with{NextProductId=long.MaxValue};Assert.True(normal.RestoreCheckpoint(c).Accepted);
        before=normal.ExportCheckpoint().CanonicalText();Assert.Throws<OverflowException>(()=>normal.Submit(Cmd(normal,CookingRecipeOperation.ServePortion,"overflow",item:Pot,container:Bowl)));
        Assert.Equal(before,normal.ExportCheckpoint().CanonicalText());
    }
    private sealed class BrokenAllocator(string identity):ICookingProductIdAllocator {public ItemId GetProductId(long sequence)=>new(identity);}

    [Fact] public void Checkpoint_pose_worker_balance_tampering_rejects_and_handoff_keeps_balance_clears_worker()
    {
        var s=Sim(manual:true,portions:3);Start(s);Tick(s,2);var checkpoint=s.ExportCheckpoint();var target=Sim(manual:true,portions:3);
        Assert.True(target.RestoreCheckpoint(checkpoint).Accepted);Assert.Equal(s.Snapshot().CanonicalText(),target.Snapshot().CanonicalText());
        Assert.False(target.RestoreCheckpoint(checkpoint with{Poses=null}).Accepted);
        Assert.False(target.RestoreCheckpoint(checkpoint with{Poses=checkpoint.Poses!.Select(p=>p with{X=9000}).ToArray()}).Accepted);
        Assert.False(target.RestoreCheckpoint(checkpoint with{Processes=checkpoint.Processes.Select(p=>p with{ActiveWorker=new PlayerId("unknown")}).ToArray()}).Accepted);
        Assert.Null(Assert.Single(s.ExportSuccessHandoff().Processes).ActiveWorker);
        Tick(s,2);Accept(s.Submit(Cmd(s,CookingRecipeOperation.ServePortion,"portion",item:Pot,container:Bowl)));
        var handoff=s.ExportSuccessHandoff();var next=Sim(manual:true,portions:3);Assert.True(next.AcceptSuccessHandoff(handoff).Accepted);
        Assert.Equal(2,next.Snapshot().Items.Single(i=>i.Id==Pot).RemainingPortions);
        Assert.False(next.RestoreCheckpoint(handoff with{Items=handoff.Items.Select(i=>i.Id==Pot?i with{RemainingPortions=4}:i).ToArray()}).Accepted);
    }

    [Fact] public void Content_preserves_manual_yield_hash_and_rejects_unimplemented_multi_consume()
    {
        var fixture=Fixture(manual:true,portions:3,spatial:false);
        var candidate=new CookingConfigurationCandidate(new[]{"work"},fixture.Items.Values.ToArray(),fixture.Appliances.Values.ToArray(),fixture.Recipes.Values.ToArray());
        var registry=new CookingConfigurationRegistry();Assert.True(registry.Submit(candidate).Accepted);
        var recipe=registry.Current!.Recipes[Recipe];Assert.Equal(CookingRecipeExecutionKind.Manual,recipe.Execution);Assert.Equal(3,recipe.YieldPortions);
        var identity=registry.Current.Identity;Assert.True(registry.Submit(candidate with{Recipes=new[]{recipe with{YieldPortions=2}}}).Accepted);Assert.NotEqual(identity,registry.Current!.Identity);
        Assert.False(registry.Submit(candidate with{Recipes=new[]{recipe with{Completion=CookingRecipeCompletionKind.ConsumeInputs}}}).Accepted);
    }
    [Fact] public void Preview_prefers_facing_before_distance_and_world_slots_are_single_objects()
    {
        var f=Fixture();var spatial=f.Spatial! with{Anchors=f.Spatial!.Anchors.Select(a=>a.Id=="spawn"?a with{X=900,Y=0}:a.Id=="clean-pool"?a with{X=300,Y=300}:a).ToArray()};
        var s=new CookingRecipeSimulation(new CookingRecipeFixture(f.Scope,f.Players,f.Items,f.Appliances,f.Recipes,spatial:spatial));
        s.AddWorldIngredient(new("angled"),Raw,"clean-pool");s.AddWorldIngredient(new("straight"),Raw,"spawn");
        var before=s.ExportCheckpoint().CanonicalText();
        var candidates=s.PreviewInteraction(A,1,new("preview"));
        Assert.True(candidates.ToList().FindIndex(p=>p.TargetId=="straight") < candidates.ToList().FindIndex(p=>p.TargetId=="angled"));
        Assert.Throws<ArgumentException>(()=>s.AddWorldIngredient(new("duplicate"),Raw,"spawn"));
        Assert.Equal(before,s.ExportCheckpoint().CanonicalText());
    }
    [Fact] public void Serving_checks_each_endpoint_and_completed_single_yield_pour_remains_legacy()
    {
        var s=Sim(portions:3);Complete(s);
        s.AddItem(new("remote-bowl"),BowlDef,ItemLocation.Station(Far));
        var before=s.Snapshot().CanonicalText();
        Assert.Equal(CookingRecipeRejectionReason.TargetOutOfRange,s.Submit(Cmd(s,CookingRecipeOperation.ServePortion,"target-far",item:Pot,container:new("remote-bowl"))).Reason);
        Assert.Equal(before,s.Snapshot().CanonicalText());
        var moved=s.ExportCheckpoint() with{Items=s.ExportCheckpoint().Items.Select(i=>i.Id==Pot?i with{Location=ItemLocation.Station(Far)}:
            i.Id==new ItemId("remote-bowl")?i with{Location=ItemLocation.World("spawn")}:i).ToArray()};
        Assert.True(s.RestoreCheckpoint(moved).Accepted);before=s.Snapshot().CanonicalText();
        Assert.Equal(CookingRecipeRejectionReason.TargetOutOfRange,s.Submit(Cmd(s,CookingRecipeOperation.ServePortion,"source-far",item:Pot,container:Bowl)).Reason);
        Assert.Equal(before,s.Snapshot().CanonicalText());
        // Legacy single-yield completion still produces exactly one product and reuses the source.
        var one=Sim();Complete(one);Accept(one.Submit(Cmd(one,CookingRecipeOperation.Pour,"single",item:Pot,container:Bowl)));
        Assert.Single(one.ItemsInContainer(Bowl));Assert.Empty(one.ItemsInContainer(Pot));
    }
    [Fact] public void Destination_spawn_replaces_old_pose_but_same_level_restore_keeps_tick_movement_cap()
    {
        var s=Sim();Accept(s.Submit(new(Scope,1,A,new("move"),CookingRecipeOperation.Move,MoveX:-1000)));
        var c=s.ExportCheckpoint();var same=Sim();Assert.True(same.RestoreCheckpoint(c).Accepted);
        Assert.Equal(CookingRecipeRejectionReason.MovementBlocked,same.Submit(new(Scope,2,A,new("again"),CookingRecipeOperation.Move,MoveX:-1000)).Reason);
        var f=Fixture();var destination=f.Spatial! with{InitialPoses=f.Spatial!.InitialPoses.Select(p=>p with{X=-2000}).ToArray()};
        var next=new CookingRecipeSimulation(new CookingRecipeFixture(f.Scope,f.Players,f.Items,f.Appliances,f.Recipes,spatial:destination));
        Assert.True(next.AcceptSuccessHandoff(s.ExportSuccessHandoff()).Accepted);
        Assert.Equal(-2000,next.Snapshot().Poses!.Single(p=>p.Player==A).X);
        var before=same.Snapshot().CanonicalText();
        Assert.False(same.RestoreCheckpoint(c with{SchemaVersion=2}).Accepted);Assert.Equal(before,same.Snapshot().CanonicalText());
    }

    [Fact] public void One_player_cannot_work_two_manual_processes_and_turning_releases_claim()
    {
        var f=Fixture(manual:true);var recipe=f.Recipes[Recipe] with{RequiresStation=false};
        var s=new CookingRecipeSimulation(new CookingRecipeFixture(f.Scope,f.Players,f.Items,f.Appliances,
            new Dictionary<RecipeId,CookingRecipeDefinition>{[Recipe]=recipe},spatial:f.Spatial));
        s.AddItem(Pot,PotDef,ItemLocation.Station(Board));s.AddItem(new("second-pot"),PotDef,ItemLocation.Station(Counter));
        s.AddItem(RawItem,Raw,ItemLocation.Container(Pot,"slot-0"));s.AddItem(new("second-raw"),Raw,ItemLocation.Container(new("second-pot"),"slot-0"));
        Accept(s.Submit(Cmd(s,CookingRecipeOperation.StartProcess,"one",item:Pot)));var before=s.Snapshot().CanonicalText();
        Assert.Equal(CookingRecipeRejectionReason.WorkerUnavailable,s.Submit(Cmd(s,CookingRecipeOperation.StartProcess,"two",item:new("second-pot"))).Reason);
        Assert.Equal(before,s.Snapshot().CanonicalText());
        Accept(s.Submit(Cmd(s,CookingRecipeOperation.StartProcess,"other",B,item:new("second-pot"))));Tick(s,1);
        Accept(s.Submit(new(Scope,1,A,new("turn"),CookingRecipeOperation.Move,FacingX:-1)));Tick(s,1);
        Assert.Null(s.Snapshot().Processes.Single(p=>p.Anchor==Pot).ActiveWorker);
        Assert.Equal(1,s.Snapshot().Processes.Single(p=>p.Anchor==Pot).ElapsedTicks);
        Assert.Equal(2,s.Snapshot().Processes.Single(p=>p.Anchor==new ItemId("second-pot")).ElapsedTicks);
        Assert.True(s.MigrateStations(new[]{new CookingStationReplacement(Board,Counter)}).Accepted);
        Assert.All(s.Snapshot().Processes,p=>Assert.Null(p.ActiveWorker));
        Assert.Equal(2,s.Snapshot().Processes.Single(p=>p.Anchor==new ItemId("second-pot")).ElapsedTicks);
    }

    [Fact] public void Existing_clean_container_dispenser_is_not_an_ordinary_world_table_and_has_anchor()
    {
        var f=Fixture();
        var s=new CookingRecipeSimulation(new CookingRecipeFixture(f.Scope,f.Players,f.Items,f.Appliances,f.Recipes,
            cleanContainerSupply:new Dictionary<DefinitionId,int>{[BowlDef]=2},spatial:f.Spatial));
        Assert.True(s.RestoreCheckpoint(s.ExportCheckpoint()).Accepted);
        Assert.Equal(2,s.Snapshot().Items.Count);
        var bad=f.Spatial! with{Anchors=f.Spatial!.Anchors.Where(a=>a.Id!="clean-pool").ToArray()};
        Assert.Throws<ArgumentException>(()=>new CookingRecipeSimulation(new CookingRecipeFixture(f.Scope,f.Players,f.Items,f.Appliances,f.Recipes,
            cleanContainerSupply:new Dictionary<DefinitionId,int>{[BowlDef]=2},spatial:bad)));
    }

    [Fact] public void Incompatible_portion_target_and_completed_batch_edits_leave_state_and_watermark_unchanged()
    {
        var f=Fixture(portions:3);var rejectDef=new DefinitionId("rejecting");
        var items=f.Items.ToDictionary(p=>p.Key,p=>p.Value);items.Add(rejectDef,new(rejectDef,new HashSet<string>{"cook"},new(4,new HashSet<DefinitionId>{Raw})));
        var s=new CookingRecipeSimulation(new CookingRecipeFixture(f.Scope,f.Players,items,f.Appliances,f.Recipes,spatial:f.Spatial));
        s.AddItem(Pot,PotDef,ItemLocation.Station(Board));s.AddItem(Bowl,rejectDef,ItemLocation.Station(Counter));
        s.AddItem(RawItem,Raw,ItemLocation.Container(Pot,"slot-0"));Complete(s);
        var before=s.Snapshot().CanonicalText();var count=s.ExportCheckpoint().NextProductId;
        Assert.Equal(CookingRecipeRejectionReason.ContainerRejectsItem,s.Submit(Cmd(s,CookingRecipeOperation.ServePortion,"incompatible",item:Pot,container:Bowl)).Reason);
        Assert.Equal(before,s.Snapshot().CanonicalText());Assert.Equal(count,s.ExportCheckpoint().NextProductId);
        s.AddItem(new("extra"),Raw,ItemLocation.Hand(A));before=s.Snapshot().CanonicalText();
        Assert.Equal(CookingRecipeRejectionReason.BatchCompleted,s.Submit(Cmd(s,CookingRecipeOperation.PutIn,"append",item:new("extra"),container:Pot)).Reason);
        Assert.Equal(CookingRecipeRejectionReason.BatchCompleted,s.Submit(Cmd(s,CookingRecipeOperation.StartProcess,"restart",item:Pot,station:Board)).Reason);
        Assert.Equal(before,s.Snapshot().CanonicalText());
        Accept(s.Submit(Cmd(s,CookingRecipeOperation.DiscardItem,"discard",item:new("extra"))));Assert.Null(s.ItemInHand(A));
        Accept(s.Submit(Cmd(s,CookingRecipeOperation.ClearContents,"clear",item:Pot)));Assert.Empty(s.ItemsInContainer(Pot));
        s.AddItem(new("fresh"),Raw,ItemLocation.Hand(A));Accept(s.Submit(Cmd(s,CookingRecipeOperation.PutIn,"refill",item:new("fresh"),container:Pot)));
        Accept(s.Submit(Cmd(s,CookingRecipeOperation.StartProcess,"reuse",item:Pot,station:Board)));Tick(s,4);
        Assert.Equal(3,s.Snapshot().Items.Single(i=>i.Id==Pot).RemainingPortions);
    }

    [Fact] public void Preview_allocation_overflow_is_nonmutating_and_does_not_escape_read_only_query()
    {
        var s=Sim(portions:3);Complete(s);Accept(s.Submit(Cmd(s,CookingRecipeOperation.Pickup,"carry",item:Pot)));
        Assert.True(s.RestoreCheckpoint(s.ExportCheckpoint() with{NextProductId=long.MaxValue}).Accepted);
        var before=s.ExportCheckpoint().CanonicalText();
        Assert.DoesNotContain(s.PreviewInteraction(A,1,new("preview")),p=>p.Command.Operation==CookingRecipeOperation.ServePortion);
        Assert.Equal(before,s.ExportCheckpoint().CanonicalText());
    }

    [Fact] public void Explicit_recipe_start_preserves_duplicate_input_counts()
    {
        var f=Fixture();var recipe=f.Recipes[Recipe] with{Inputs=new[]{Raw,Raw}};
        var s=new CookingRecipeSimulation(new CookingRecipeFixture(f.Scope,f.Players,f.Items,f.Appliances,
            new Dictionary<RecipeId,CookingRecipeDefinition>{[Recipe]=recipe},spatial:f.Spatial));
        s.AddItem(Pot,PotDef,ItemLocation.Station(Board));s.AddItem(RawItem,Raw,ItemLocation.Container(Pot,"slot-0"));
        var before=s.Snapshot().CanonicalText();
        Assert.Equal(CookingRecipeRejectionReason.RecipeNotMatched,s.Submit(Cmd(s,CookingRecipeOperation.StartProcess,"missing",item:Pot,station:Board) with{Recipe=Recipe}).Reason);
        Assert.Equal(before,s.Snapshot().CanonicalText());
        s.AddItem(new("second"),Raw,ItemLocation.Container(Pot,"slot-1"));
        Accept(s.Submit(Cmd(s,CookingRecipeOperation.StartProcess,"counted",item:Pot,station:Board) with{Recipe=Recipe}));Tick(s,4);
        Assert.Equal(2,s.ItemsInContainer(Pot).Count);Assert.True(s.RestoreCheckpoint(s.ExportCheckpoint()).Accepted);
    }

}
