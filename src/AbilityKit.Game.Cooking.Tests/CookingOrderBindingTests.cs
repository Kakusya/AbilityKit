using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

[Trait("Gate", "CookingKitchenLoop")]
public sealed class CookingOrderBindingTests
{
    private static readonly CookingScope Scope = new(new("binding-session"), new("binding-world"), new("binding-match"));
    private static readonly CookingLevelScope Level = new(Scope, new(1), new("level"), 1);
    private static readonly PlayerId A = new("a"), B = new("b"), Restricted = new("restricted");
    private static readonly StationSlotId Stove = new("stove"), Counter = new("counter"), Far = new("far");
    private static readonly DefinitionId Raw = new("raw"), Food = new("food"), Cup = new("cup"), WrongCup = new("wrong-cup");
    private static readonly RecipeId Recipe = new("recipe"), WrongRecipe = new("wrong-recipe"), Reprocess = new("reprocess");
    private static readonly OrderTemplateId Drink = new("drink"), Meal = new("meal"), Foreign = new("foreign");
    private static readonly OrderId One = new("one"), Two = new("two");

    private static CookingRecipeSimulation Create(bool disposable = false, bool otherReachable = true)
    {
        var fixture = new CookingRecipeFixture(Scope,
            new Dictionary<PlayerId, CookingPlayerConfig>
            {
                [A] = new(A, new HashSet<string>{"cook"}, new HashSet<string>{"stove", "counter"}),
                [B] = new(B, new HashSet<string>{"cook"}, otherReachable ? new HashSet<string>{"stove", "counter"} : new HashSet<string>{"stove"}),
                [Restricted] = new(Restricted, new HashSet<string>{"guest"}, new HashSet<string>{"stove", "counter"}),
            },
            new Dictionary<DefinitionId, CookingItemDefinition>
            {
                [Raw] = new(Raw, new HashSet<string>{"cook"}), [Food] = new(Food, new HashSet<string>{"cook"}),
                [Cup] = new(Cup, new HashSet<string>{"cook"}, new(1, new HashSet<DefinitionId>{Food}, disposable)),
                [WrongCup] = new(WrongCup, new HashSet<string>{"cook"}, new(1, new HashSet<DefinitionId>{Food})),
            },
            new Dictionary<StationSlotId, CookingApplianceDefinition>
            {
                [Stove] = new(Stove, new HashSet<string>{"heat"}), [Counter] = new(Counter, new HashSet<string>{"heat"}),
                [Far] = new(Far, new HashSet<string>()),
            },
            new Dictionary<RecipeId, CookingRecipeDefinition>
            {
                [Recipe] = new(Recipe, new[]{Raw}, Food, new("heat"), "heat", 1),
                [WrongRecipe] = new(WrongRecipe, new[]{Raw}, Food, new("other"), "heat", 1),
                [Reprocess] = new(Reprocess, new[]{Food}, Food, new("reprocess"), "heat", 2, Completion:CookingRecipeCompletionKind.RetainInputs),
            },
            orderTemplates: new Dictionary<OrderTemplateId, CookingOrderTemplateDefinition>
            {
                [Drink] = new(Drink, Recipe, Cup, RequiresBinding:true),
                [Meal] = new(Meal, Recipe, Cup), [Foreign] = new(Foreign, WrongRecipe, Cup),
            });
        return new(fixture);
    }

    private static CookingRecipeCommand Cmd(CookingRecipeSimulation s, CookingRecipeOperation op, string id,
        ItemId item, OrderId? order = null, PlayerId? player = null) =>
        new(Scope, 1, player ?? A, new(id), op, Item:item, Order:order,
            ExpectedItemVersion:s.Snapshot().Items.Single(i=>i.Id==item).Version);
    private static void Accept(CookingRecipeCommandResult result) =>
        Assert.True(result.Outcome == CookingRecipeOutcome.Accepted, result.Reason.ToString());
    private static void Reject(CookingRecipeSimulation s, CookingRecipeCommand cmd, CookingRecipeRejectionReason? reason = null)
    {
        var before = s.Snapshot().CanonicalText();
        var result = s.Submit(cmd);
        Assert.Equal(CookingRecipeOutcome.Rejected, result.Outcome);
        if (reason is {} expected) Assert.Equal(expected, result.Reason);
        Assert.Empty(result.Events);
        Assert.Equal(before, s.Snapshot().CanonicalText());
    }
    private static ItemId Produce(CookingRecipeSimulation s, string suffix, DefinitionId? vessel = null, RecipeId? recipe = null)
    {
        var input = new ItemId("raw-" + suffix);
        var cup = new ItemId("cup-" + suffix);
        s.AddItem(cup, vessel ?? Cup, ItemLocation.Station(Counter));
        s.AddItem(input, Raw, ItemLocation.Station(Stove));
        Accept(s.Submit(new(Scope, 1, A, new("start-" + suffix), CookingRecipeOperation.StartProcess,
            Recipe:recipe ?? Recipe, Item:input, Station:Stove, ExpectedItemVersion:1)));
        s.AdvanceFixedTick(Level, s.LogicalTick + 1);
        var product = s.Snapshot().Items.Single(i=>i.IsProduct && i.Location == ItemLocation.Station(Stove)).Id;
        Accept(s.Submit(Cmd(s, CookingRecipeOperation.Pickup, "pickup-" + suffix, product)));
        Accept(s.Submit(Cmd(s, CookingRecipeOperation.PutIn, "plate-" + suffix, product) with{Container=cup}));
        return product;
    }
    private static OrderId? Binding(CookingRecipeSimulation s, ItemId product) => s.Snapshot().Items.Single(i=>i.Id==product).BoundOrder;

    [Fact]
    public void Bind_unbind_rebind_change_versions_and_rejected_rebind_preserves_food_and_old_order()
    {
        var s=Create(); var food=Produce(s,"one");
        Assert.True(s.OpenOrder(One,Drink).Accepted); Assert.True(s.OpenOrder(Two,Drink).Accepted);
        var initial=s.Snapshot().Items.Single(i=>i.Id==food).Version;
        Accept(s.Submit(Cmd(s,CookingRecipeOperation.BindOrder,"bind",food,One)));
        Assert.Equal(One,Binding(s,food)); Assert.Equal(initial+1,s.Snapshot().Items.Single(i=>i.Id==food).Version);
        Reject(s,Cmd(s,CookingRecipeOperation.RebindOrder,"missing",food,new("missing")),CookingRecipeRejectionReason.OrderNotFound);
        Assert.Equal(One,Binding(s,food));
        Accept(s.Submit(Cmd(s,CookingRecipeOperation.RebindOrder,"rebind",food,Two)));
        Assert.Equal(Two,Binding(s,food));
        Accept(s.Submit(Cmd(s,CookingRecipeOperation.UnbindOrder,"unbind",food)));
        Assert.Null(Binding(s,food)); Assert.Equal(initial+3,s.Snapshot().Items.Single(i=>i.Id==food).Version);
        Assert.Single(s.ItemsInContainer(new("cup-one"))); Assert.Empty(s.SettlementHistory);
    }

    [Fact]
    public void Binding_rejects_stale_scope_version_missing_order_wrong_recipe_wrong_vessel_and_raw_food()
    {
        var s=Create(); var food=Produce(s,"one"); Assert.True(s.OpenOrder(One,Drink).Accepted);
        var bind=Cmd(s,CookingRecipeOperation.BindOrder,"scope",food,One);
        Reject(s,bind with{Scope=Scope with{Match=new("old")}},CookingRecipeRejectionReason.ScopeMismatch);
        Reject(s,bind with{Command=new("stale"),ExpectedItemVersion=1},CookingRecipeRejectionReason.ItemStale);
        Reject(s,bind with{Command=new("missing"),Order=new("missing")},CookingRecipeRejectionReason.OrderNotFound);
        Assert.True(s.OpenOrder(Two,Foreign).Accepted);
        Reject(s,bind with{Command=new("wrong-recipe"),Order=Two},CookingRecipeRejectionReason.OrderRequirementMismatch);
        var wrong=Produce(s,"wrong",WrongCup);
        Reject(s,Cmd(s,CookingRecipeOperation.BindOrder,"wrong-vessel",wrong,One),CookingRecipeRejectionReason.OrderRequirementMismatch);
        s.AddItem(new("unprocessed"),Raw,ItemLocation.Station(Counter));
        Reject(s,Cmd(s,CookingRecipeOperation.BindOrder,"raw",new("unprocessed"),One));
    }

    [Fact]
    public void Two_players_contend_for_one_product_and_one_order_with_only_one_binding()
    {
        var s=Create(); var food=Produce(s,"one"); Assert.True(s.OpenOrder(One,Drink).Accepted); Assert.True(s.OpenOrder(Two,Drink).Accepted);
        var a=Cmd(s,CookingRecipeOperation.BindOrder,"race",food,One);
        var results=s.SubmitBatch(new[]{a with{Player=B,Order=Two},a});
        Assert.Single(results,r=>r.Outcome==CookingRecipeOutcome.Accepted); Assert.Equal(One,Binding(s,food));
        var other=Produce(s,"two");
        Reject(s,Cmd(s,CookingRecipeOperation.BindOrder,"occupied-order",other,One,B),CookingRecipeRejectionReason.BindingConflict);
        Assert.Null(Binding(s,other));
        Accept(s.Submit(Cmd(s,CookingRecipeOperation.BindOrder,"bind-second",other,Two,B)));
        Reject(s,Cmd(s,CookingRecipeOperation.RebindOrder,"occupied-target",food,Two),CookingRecipeRejectionReason.BindingConflict);
        Assert.Equal(One,Binding(s,food)); Assert.Equal(Two,Binding(s,other));
        Reject(s,Cmd(s,CookingRecipeOperation.BindOrder,"occupied-product",food,Two),CookingRecipeRejectionReason.BindingConflict);
    }

    [Fact]
    public void Required_drink_binding_and_bound_order_are_enforced_while_meals_submit_directly_once()
    {
        var s=Create(); var food=Produce(s,"one"); Assert.True(s.OpenOrder(One,Drink).Accepted); Assert.True(s.OpenOrder(Two,Drink).Accepted);
        Reject(s,Cmd(s,CookingRecipeOperation.SubmitOrder,"unbound",food,One),CookingRecipeRejectionReason.BindingRequired);
        Accept(s.Submit(Cmd(s,CookingRecipeOperation.BindOrder,"bind",food,One)));
        Reject(s,Cmd(s,CookingRecipeOperation.SubmitOrder,"wrong-order",food,Two),CookingRecipeRejectionReason.BindingConflict);
        var submit=Cmd(s,CookingRecipeOperation.SubmitOrder,"submit",food,One,B);
        Accept(s.Submit(submit)); Assert.True(s.Submit(submit).IsDuplicate); Assert.Single(s.SettlementHistory);
        var next=Produce(s,"two");
        Reject(s,Cmd(s,CookingRecipeOperation.BindOrder,"completed",next,One),CookingRecipeRejectionReason.OrderAlreadyCompleted);
        var meal=Create(); var mealFood=Produce(meal,"meal"); Assert.True(meal.OpenOrder(One,Meal).Accepted);
        Accept(meal.Submit(Cmd(meal,CookingRecipeOperation.SubmitOrder,"meal-submit",mealFood,One)));
        Assert.Single(meal.SettlementHistory);
    }

    [Fact]
    public void Unbound_finished_goods_can_be_shared_and_disposable_submission_releases_hand_without_washing()
    {
        var s=Create(disposable:true); var food=Produce(s,"one"); var cup=new ItemId("cup-one");
        Accept(s.Submit(Cmd(s,CookingRecipeOperation.Pickup,"share-pickup",cup)));
        Accept(s.Submit(Cmd(s,CookingRecipeOperation.Drop,"share-drop",cup) with{Station=Counter}));
        Accept(s.Submit(Cmd(s,CookingRecipeOperation.Pickup,"other-pickup",cup,player:B)));
        Assert.True(s.OpenOrder(One,Drink).Accepted);
        Accept(s.Submit(Cmd(s,CookingRecipeOperation.BindOrder,"other-bind",food,One,B)));
        Accept(s.Submit(Cmd(s,CookingRecipeOperation.SubmitOrder,"other-submit",food,One,B)));
        Assert.Null(s.ItemInHand(B)); Assert.Empty(s.DirtyBowlsAwaitingWash());
        Assert.DoesNotContain(s.Snapshot().Items,i=>i.Id==cup || i.Id==food);
        var tombstone=s.ExportCheckpoint().Items.Single(i=>i.Id==cup); Assert.True(tombstone.Removed); Assert.False(tombstone.IsDirty);
    }

    [Fact]
    public void Every_binding_operation_rechecks_reach_and_bound_food_requires_explicit_unbind_before_removal()
    {
        var s=Create(otherReachable:false); var food=Produce(s,"one"); var cup=new ItemId("cup-one");
        Assert.True(s.OpenOrder(One,Drink).Accepted); Assert.True(s.OpenOrder(Two,Drink).Accepted);
        Reject(s,Cmd(s,CookingRecipeOperation.BindOrder,"far-bind",food,One,B),CookingRecipeRejectionReason.TargetOutOfRange);
        Accept(s.Submit(Cmd(s,CookingRecipeOperation.BindOrder,"bind",food,One)));
        Reject(s,Cmd(s,CookingRecipeOperation.UnbindOrder,"far-unbind",food,player:B),CookingRecipeRejectionReason.TargetOutOfRange);
        Reject(s,Cmd(s,CookingRecipeOperation.RebindOrder,"far-rebind",food,Two,B),CookingRecipeRejectionReason.TargetOutOfRange);
        Reject(s,Cmd(s,CookingRecipeOperation.TakeOut,"bound-take",food) with{Container=cup},CookingRecipeRejectionReason.BindingConflict);
        Accept(s.Submit(Cmd(s,CookingRecipeOperation.Pickup,"carry",cup)));
        Accept(s.Submit(Cmd(s,CookingRecipeOperation.Drop,"drop",cup) with{Station=Counter}));
        Assert.Equal(One,Binding(s,food));
        Accept(s.Submit(Cmd(s,CookingRecipeOperation.UnbindOrder,"unbind",food)));
        Accept(s.Submit(Cmd(s,CookingRecipeOperation.TakeOut,"take",food) with{Container=cup}));
        Assert.Equal(food,s.ItemInHand(A));
    }

    [Fact]
    public void Cancellation_clears_binding_increments_product_version_and_preserves_food()
    {
        var s=Create(); var food=Produce(s,"one"); Assert.True(s.OpenOrder(One,Drink).Accepted);
        Accept(s.Submit(Cmd(s,CookingRecipeOperation.BindOrder,"bind",food,One)));
        var product=s.Snapshot().Items.Single(i=>i.Id==food);
        Assert.True(s.MarkOrderUnsatisfied(One).Accepted);
        var after=s.Snapshot().Items.Single(i=>i.Id==food);
        Assert.Null(after.BoundOrder); Assert.Equal(product.Version+1,after.Version);
        Assert.Equal(product.Location,after.Location); Assert.True(after.IsProduct); Assert.Empty(s.SettlementHistory);
        Assert.False(s.MarkOrderUnsatisfied(One).Accepted);
        Assert.Equal(after.Version,s.Snapshot().Items.Single(i=>i.Id==food).Version);
    }

    [Theory]
    [InlineData(CookingRecipeOperation.UnbindOrder)]
    [InlineData(CookingRecipeOperation.RebindOrder)]
    public void Unbind_and_rebind_reject_stale_scope_version_and_ineligible_player(CookingRecipeOperation operation)
    {
        var s=Create(); var food=Produce(s,"one"); Assert.True(s.OpenOrder(One,Drink).Accepted); Assert.True(s.OpenOrder(Two,Drink).Accepted);
        Reject(s,Cmd(s,CookingRecipeOperation.BindOrder,"restricted-bind",food,One,Restricted),CookingRecipeRejectionReason.PlayerIneligible);
        Accept(s.Submit(Cmd(s,CookingRecipeOperation.BindOrder,"bind",food,One)));
        var command=Cmd(s,operation,"stale",food,operation==CookingRecipeOperation.RebindOrder?Two:null);
        Reject(s,command with{ExpectedItemVersion=command.ExpectedItemVersion-1},CookingRecipeRejectionReason.ItemStale);
        Reject(s,command with{Command=new("old-scope"),Scope=Scope with{Match=new("old")}},CookingRecipeRejectionReason.ScopeMismatch);
        Reject(s,command with{Command=new("restricted"),Player=Restricted},CookingRecipeRejectionReason.PlayerIneligible);
        Assert.Equal(One,Binding(s,food));
    }

    [Fact]
    public void Rebind_wrong_recipe_and_completed_target_preserves_original_binding()
    {
        var s=Create(); var food=Produce(s,"one"); Assert.True(s.OpenOrder(One,Drink).Accepted);
        var foreign=new OrderId("foreign"); Assert.True(s.OpenOrder(foreign,Foreign).Accepted);
        Assert.True(s.OpenOrder(Two,Meal).Accepted); var delivered=Produce(s,"two");
        Accept(s.Submit(Cmd(s,CookingRecipeOperation.SubmitOrder,"complete-target",delivered,Two)));
        Accept(s.Submit(Cmd(s,CookingRecipeOperation.BindOrder,"bind",food,One)));
        Reject(s,Cmd(s,CookingRecipeOperation.RebindOrder,"wrong-recipe",food,foreign),CookingRecipeRejectionReason.OrderRequirementMismatch);
        Reject(s,Cmd(s,CookingRecipeOperation.RebindOrder,"closed-target",food,Two),CookingRecipeRejectionReason.OrderAlreadyCompleted);
        Assert.Equal(One,Binding(s,food)); Assert.Single(s.ItemsInContainer(new("cup-one")));
    }

    [Fact]
    public void Active_process_input_cannot_be_bound_or_submitted_as_a_finished_product()
    {
        var s=Create(); var food=Produce(s,"one"); var cup=new ItemId("cup-one"); Assert.True(s.OpenOrder(One,Meal).Accepted);
        Accept(s.Submit(Cmd(s,CookingRecipeOperation.StartProcess,"reprocess",cup) with{Recipe=Reprocess,Station=Counter}));
        Assert.Single(s.Snapshot().Processes);
        Reject(s,Cmd(s,CookingRecipeOperation.BindOrder,"locked-bind",food,One),CookingRecipeRejectionReason.ItemStale);
        Reject(s,Cmd(s,CookingRecipeOperation.SubmitOrder,"locked-submit",food,One),CookingRecipeRejectionReason.ItemStale);
        Assert.Null(Binding(s,food)); Assert.Empty(s.SettlementHistory);
    }

    [Fact]
    public void Bindings_roundtrip_in_canonical_checkpoint_and_invalid_ownership_rejects_atomically()
    {
        var s=Create(); var food=Produce(s,"one"); var other=Produce(s,"two"); Assert.True(s.OpenOrder(One,Drink).Accepted);
        var unbound=s.Snapshot().CanonicalText(); Accept(s.Submit(Cmd(s,CookingRecipeOperation.BindOrder,"bind",food,One)));
        Assert.NotEqual(unbound,s.Snapshot().CanonicalText()); Assert.Contains("boundOrder",s.Snapshot().CanonicalText(),StringComparison.OrdinalIgnoreCase);
        var checkpoint=s.ExportCheckpoint(); var restored=Create(); Assert.True(restored.RestoreCheckpoint(checkpoint).Accepted);
        Assert.Equal(s.Snapshot().CanonicalText(),restored.Snapshot().CanonicalText()); Assert.Equal(One,Binding(restored,food));
        var before=restored.Snapshot().CanonicalText();
        foreach (var tampered in new[]{
            checkpoint with{Items=checkpoint.Items.Select(i=>i.Id==other?i with{BoundOrder=One}:i).ToArray()},
            checkpoint with{Items=checkpoint.Items.Select(i=>i.Id==food?i with{BoundOrder=new("absent")}:i).ToArray()},
            checkpoint with{Items=checkpoint.Items.Select(i=>i.Id==food?i with{IsProduct=false}:i).ToArray()},
            checkpoint with{Containers=checkpoint.Containers.Select(c=>c.Id==new ItemId("cup-one")?c with{ItemIds=Array.Empty<ItemId>()}:c).ToArray()},
        })
        {
            Assert.False(restored.RestoreCheckpoint(tampered).Accepted); Assert.Equal(before,restored.Snapshot().CanonicalText());
        }
        var handoff=s.ExportSuccessHandoff(); Assert.All(handoff.Items,i=>Assert.Null(i.BoundOrder));
        Accept(restored.Submit(Cmd(restored,CookingRecipeOperation.ClearContents,"dispose",new("cup-one"))));
        Assert.DoesNotContain(restored.Snapshot().Items,i=>i.Id==food);
        Assert.DoesNotContain(restored.ExportCheckpoint().Items,i=>i.BoundOrder is not null);
    }
}
