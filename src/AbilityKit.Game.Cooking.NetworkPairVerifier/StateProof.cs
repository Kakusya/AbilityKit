using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AbilityKit.Game.Cooking.RichEvidence;
using AbilityKit.Game.Cooking.Session;
namespace AbilityKit.Game.Cooking.NetworkPairVerifier;

internal static class StateProof
{
    public static string TextHash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    public static void Full(CookingNetworkAuthorityCapture capture)
    {
        var full = capture.FullRecipe ?? throw new InvalidOperationException("Missing full graph.");
        Check.That(full.SchemaVersion == 5 && capture.Observation.Scope == full.LevelScope, "Current complete Recipe5 scope.");
        Check.That(full.Items.Select(x => x.Id).Distinct().Count() == full.Items.Count && full.Deduplication.Select(x => (x.Session, x.Player, x.Command)).Distinct().Count() == full.Deduplication.Count, "Unique objects/receipts.");
        foreach (var slot in full.Items.Where(x => !x.Removed && x.Location.Kind is LocationKind.PlayerHand or LocationKind.WorldPosition or LocationKind.StationSlot).GroupBy(x => x.Location)) Check.That(slot.Count() == 1, "Single ordinary placement/hand ownership.");
        Check.That(full.Containers.Select(x => x.Id).Distinct().Count() == full.Containers.Count, "Unique container rows.");
        foreach (var child in full.Items.Where(x => !x.Removed && x.Location.Kind == LocationKind.ContainerSlot)) {
            var parent = full.Items.SingleOrDefault(x => x.Id.Value == child.Location.OwnerId && !x.Removed);
            Check.That(parent is not null && full.Containers.Count(x => x.Id == parent.Id && x.ItemIds.Contains(child.Id)) == 1, "Every live contained item has actual live container membership, no orphan.");
        }
        foreach (var container in full.Containers) {
            Check.That(full.Items.Any(x => x.Id == container.Id), "Container row anchors an actual item.");
            var actual = full.Items.Where(x => !x.Removed && x.Location.Kind == LocationKind.ContainerSlot && x.Location.OwnerId == container.Id.Value).Select(x => x.Id).ToArray();
            Check.That(container.ItemIds.Distinct().Count() == container.ItemIds.Count && actual.Length == container.ItemIds.Count && container.ItemIds.All(actual.Contains), "Complete container membership.");
        }
        Check.That(full.NextProductId >= 0 && full.NextProcessId >= 0 && full.NextSettlementSequence == full.Settlements.Count && full.Items.All(x => x.AllocationSequence >= 0 && x.AllocationSequence <= full.NextProductId) && full.Items.Where(x => x.AllocationSequence > 0).Select(x => x.AllocationSequence).Distinct().Count() == full.Items.Count(x => x.AllocationSequence > 0) && full.Settlements.All(x => x.Sequence > 0 && x.Sequence <= full.NextSettlementSequence), "Allocator watermark below live/tombstone allocation.");
        Check.That(full.ConsumedProducts.All(id => full.Items.Any(x => x.Id == id && x.Removed)), "Consumed product tombstones retained.");
        Check.That(JsonSerializer.SerializeToUtf8Bytes(capture, CookingNetworkWireCodec.JsonOptions).Length <= 8 * 1024 * 1024, "Individual full evidence8MiB bound.");
    }
    public static void Compatibility(CookingNetworkAuthorityCapture capture, IReadOnlyList<RichContainerRule> rules)
    {
        Check.That(rules.Select(x => x.Definition).Distinct().Count() == rules.Count && rules.All(x => x.Capacity > 0 && x.Accepted.Distinct().Count() == x.Accepted.Count), "Actual unique exported container capability rules.");
        foreach (var row in capture.FullRecipe!.Containers) {
            var anchor = capture.FullRecipe.Items.Single(x => x.Id == row.Id); var rule = rules.Single(x => x.Definition == anchor.Definition);
            Check.That(row.ItemIds.Count <= rule.Capacity && row.ItemIds.All(id => rule.Accepted.Contains(capture.FullRecipe.Items.Single(x => x.Id == id).Definition)), "Every full container content compatible with actual configured capacity/materials.");
        }
    }
    public static void Capture(RichCapture evidence)
    {
        Full(evidence.Capture);
        Check.That(evidence.BusinessHash == CookingNetworkWireCodec.Hash(evidence.Capture) && evidence.RecipeCanonicalHash == TextHash(evidence.Capture.FullRecipe!.CanonicalText()), "Recomputed full business/recipe canonical hash.");
        Check.That(evidence.FrontCanonicalHash == (evidence.Capture.FullFront is null ? null : TextHash(evidence.Capture.FullFront.CanonicalText())), "Recomputed front canonical hash.");
    }
    public static void Baseline(CookingNetworkBaseline baseline)
    {
        Full(baseline.State);
        Check.That(baseline.Identity.StateHash == CookingNetworkWireCodec.BaselineHash(baseline.State, baseline.Session) && baseline.Identity.Scope == baseline.State.Observation.Scope && baseline.Identity.ServerSessionInstance == baseline.Session.ServerSessionInstance, "Recomputed recipient combined baseline hash/metadata.");
        Check.That(baseline.LevelFormatVersion == CookingLevelCheckpointCodec.CurrentFormatVersion && baseline.RecipeSchemaVersion == 5 && baseline.Identity.Epoch == baseline.Identity.Scope.LevelEpoch, "Current supported baseline format/epoch.");
        Check.That(baseline.Session.Participants.Count == 2 && baseline.Session.Participants.Select(x => x.Participant).Distinct().Count() == 2 && baseline.Session.Participants.All(x => x.ConnectionGeneration >= 0), "Unique Session participant rows/valid generations.");
        var participant = baseline.Session.Participants.Single(x => x.Participant == baseline.Identity.Participant);
        Check.That(participant.ConnectionGeneration == baseline.Identity.ConnectionGeneration, "Current baseline participant generation.");
    }
    public static void Caller(RichCallerCheckpoint caller)
    {
        Baseline(caller.Baseline); Baseline(caller.GrantedBaseline);
        Check.Equal(caller.Baseline.Identity, caller.ExactAckSent, "Actual latest image ACK.");
        Check.Equal(caller.GrantedBaseline.Identity, caller.ActualReady, "Ready matches retained validated image.");
        Check.That(caller.Baseline.Identity.Participant == caller.Participant && caller.GrantedBaseline.Identity.Participant == caller.Participant && caller.Participant == caller.ActualReady.Participant && caller.ExactAckSent.ServerSessionInstance == caller.ActualReady.ServerSessionInstance && caller.ExactAckSent.Scope == caller.ActualReady.Scope && caller.ExactAckSent.ConnectionGeneration == caller.ActualReady.ConnectionGeneration, "Running grant belongs to current binding/scope, not historical image.");
        Check.That(caller.BaselineReceiveOrdinal > 0 && caller.AckSendOrdinal > caller.BaselineReceiveOrdinal && caller.ReadyReceiveOrdinal > 0, "Actual caller receive/ACK/Ready ordering.");
    }
    public static string Physical(CookingRecipeCheckpoint full) => Check.Canonical(JsonSerializer.SerializeToElement(new { full.Items, full.Containers, full.Processes, full.NextProductId, full.NextProcessId, full.NextSettlementSequence, full.Settlements, full.ConsumedProducts, full.CleanContainerCounts, full.Supply, full.SupplyOrigins }, CookingNetworkWireCodec.JsonOptions));
    public static void Natural(CookingNetworkAuthorityCapture capture)
    {
        Full(capture); var front = capture.FullFront?.State ?? throw new InvalidOperationException("Natural complete front missing.");
        Check.That(capture.Observation.Lifecycle.State == CookingLevelState.Ended && capture.FullRecipe!.Settlements.Count == 2 && capture.FullRecipe.Orders.Count(x => x.Status == CookingOrderStatus.Completed) == 2 && capture.FullRecipe.Processes.Count == 0 && front.UnsatisfiedOrders.Count == 1 && capture.Observation.Recipe is { IsCompleted: true, Stars: 0 }, "Natural2deliveries1unmet0stars.");
        Check.That(front.Closing && front.WashQueue.Count == 0 && front.Tables.All(x => x.State == CookingFrontTableState.Free) && front.Work.All(x => x.Player is null && x.Status != CookingFrontWorkStatus.Working), "Complete natural cleanup.");
    }
}
