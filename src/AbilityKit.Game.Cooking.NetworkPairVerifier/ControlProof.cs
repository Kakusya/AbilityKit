using System.Text.Json;
using AbilityKit.Game.Cooking.Session;
namespace AbilityKit.Game.Cooking.NetworkPairVerifier;

internal static class ControlProof
{
    public static string Physical(CookingNetworkAuthorityCapture capture) => JsonSerializer.Serialize(new {
        capture.FullRecipe!.Items, capture.FullRecipe.Containers, capture.FullRecipe.Processes,
        capture.FullRecipe.NextProductId, capture.FullRecipe.NextProcessId, capture.FullRecipe.NextSettlementSequence,
        capture.FullRecipe.ConsumedProducts, capture.FullRecipe.CleanContainerCounts
    }, CookingNetworkWireCodec.JsonOptions);
    public static void ValidateFull(CookingNetworkAuthorityCapture capture)
    {
        var full = capture.FullRecipe ?? throw new InvalidOperationException("Missing full recipe.");
        ControlEvidence.Require(full.Items.Count == ControlEvidence.ItemIds.Length && full.Items.Select(x => x.Id).Distinct().Count() == full.Items.Count, "No item copies/loss.");
        ControlEvidence.Require(full.Items.All(x => !x.Removed) && full.NextProductId == 0 && full.NextProcessId == 0 && full.Processes.Count == 0, "No invented product/process/tombstone.");
        foreach (var group in full.Items.Where(x => x.Location.Kind is LocationKind.PlayerHand or LocationKind.StationSlot or LocationKind.WorldPosition).GroupBy(x => x.Location))
            ControlEvidence.Require(group.Count() == 1, "Single ordinary placement/hand occupancy.");
        foreach (var container in full.Containers) {
            var contained = full.Items.Where(x => x.Location.Kind == LocationKind.ContainerSlot && x.Location.OwnerId == container.Id.Value).Select(x => x.Id).ToArray();
            ControlEvidence.Require(container.ItemIds.Count == contained.Length && container.ItemIds.All(contained.Contains), "Complete container contents.");
        }
        ControlEvidence.Require(full.Deduplication.Select(x => (x.Player, x.Command)).Distinct().Count() == full.Deduplication.Count, "Unique retained receipts.");
    }
    public static void RemoteOutcome(int phase, CookingNetworkWireResult result)
    {
        var reject = phase switch { 11 => "ScopeMismatch", 12 or 17 => "ServerInstanceMismatch", 13 => "MalformedCommand", 15 => "CommandIdentityConflict", _ => null };
        if (reject is not null) {
            ControlEvidence.Require(result.Reason == reject, "Exact stale guard phase " + phase);
            if (phase == 15) ControlEvidence.Require(result.Result is { Outcome: CookingRecipeOutcome.Rejected, Reason: CookingRecipeRejectionReason.CommandIdentityConflict }, "Real cached-identity conflict terminal.");
            else ControlEvidence.Require(result.Result is null, "Early guard has no domain execution.");
            return;
        }
        ControlEvidence.Require(result.Result is not null, "Genuine domain terminal phase " + phase);
        if (phase == 7) ControlEvidence.Require(result.Result!.Outcome == CookingRecipeOutcome.Rejected && result.Result.Reason == CookingRecipeRejectionReason.ContainerRejectsItem, "Incompatible material preserved rejection.");
        else if (phase is not (1 or 4 or 20 or 21)) ControlEvidence.Require(result.Result!.Outcome == CookingRecipeOutcome.Accepted, "Legal operation phase " + phase);
        if (phase is 16 or 20 or 21) ControlEvidence.Require(result.Result!.IsDuplicate, "Current-generation cached identity.");
    }
    public static void Frame(int phase, CookingNetworkAuthorityCapture before, CookingNetworkAuthorityCapture after, CookingNetworkOwnerFrameResult frame, CookingNetworkWireResult local, CookingNetworkWireResult remote)
    {
        ValidateFull(before); ValidateFull(after); RemoteOutcome(phase, remote);
        ControlEvidence.Require(frame.Accepted && local.Result is not null, "Actual owner frame/local domain terminal.");
        var mappedRemote = phase is not (11 or 12 or 13 or 15 or 16 or 17 or 20 or 21);
        if (phase is 20 or 21) {
            ControlEvidence.Require(frame.Admissions.Count == 0 && local.Result!.IsDuplicate, "Both contested cached identities bypass new authority admission.");
            ControlEvidence.Require(Physical(before) == Physical(after) && before.FullRecipe!.Deduplication.SequenceEqual(after.FullRecipe!.Deduplication), "Both cached arbitration receipts/complete items/slots unchanged.");
            return;
        }
        ControlEvidence.Require(frame.Admissions.Count(x => x.CorrelationId == "local-" + phase) == 1 && frame.Admissions.Single(x => x.CorrelationId == "local-" + phase).Accepted, "Local actual admission.");
        ControlEvidence.Require(frame.Admissions.Count == (mappedRemote ? 2 : 1), "Exact expected authority admission prefix.");
        if (mappedRemote) ControlEvidence.Require(frame.Admissions.Count(x => x.CorrelationId == "remote-" + phase) == 1 && frame.Admissions.Single(x => x.CorrelationId == "remote-" + phase).Accepted, "Both mapped in same owner prefix.");
        else ControlEvidence.Require(frame.Admissions.All(x => x.CorrelationId != "remote-" + phase), "Early reject/cached path cannot be labelled new authority admission.");
        if (phase is 1 or 4) {
            var outcomes = new[] { local.Result!.Outcome, remote.Result!.Outcome };
            ControlEvidence.Require(outcomes.Count(x => x == CookingRecipeOutcome.Accepted) == 1 && outcomes.Count(x => x == CookingRecipeOutcome.Rejected) == 1, "Exactly one arbitration winner.");
            var winner = local.Result.Outcome == CookingRecipeOutcome.Accepted ? ControlEvidence.Chef : ControlEvidence.Partner;
            var loser = winner == ControlEvidence.Chef ? ControlEvidence.Partner : ControlEvidence.Chef;
            if (phase == 1) {
                ControlEvidence.Require(ControlEvidence.Item(after, ControlEvidence.Shared).Location == ItemLocation.Hand(winner) && !after.FullRecipe!.Items.Any(x => x.Location == ItemLocation.Hand(loser)), "Single shared item winner.");
            } else {
                var losing = before.FullRecipe!.Items.Single(x => x.Location == ItemLocation.Hand(loser));
                ControlEvidence.Require(ControlEvidence.Item(after, losing.Id) == losing, "Losing held item full record unchanged.");
                var winnerItem = before.FullRecipe.Items.Single(x => x.Location == ItemLocation.Hand(winner));
                ControlEvidence.Require(ControlEvidence.Item(after, winnerItem.Id).Location == ItemLocation.Station(ControlEvidence.Slot) && after.FullRecipe!.Items.Count(x => x.Location == ItemLocation.Station(ControlEvidence.Slot)) == 1, "Single occupied target slot.");
            }
        } else ControlEvidence.Require(local.Result!.Outcome == CookingRecipeOutcome.Accepted, "Local independent legal work.");
        if (phase == 3) ControlEvidence.Require(ControlEvidence.Item(after, ControlEvidence.ChefFood).Location == ItemLocation.Hand(ControlEvidence.Chef) && ControlEvidence.Item(after, ControlEvidence.PartnerFood).Location == ItemLocation.Hand(ControlEvidence.Partner), "Both independent pickups.");
        if (phase is 7 or 11 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19) ControlEvidence.Require(Physical(before) == Physical(after), "Rejection/retry/movement preserves full items/slots/containers/allocators/tombstones.");
        var remoteBefore = before.FullRecipe!.Deduplication.Where(x => x.Player == ControlEvidence.Partner).ToArray();
        var remoteAfter = after.FullRecipe!.Deduplication.Where(x => x.Player == ControlEvidence.Partner).ToArray();
        ControlEvidence.Require(remoteBefore.All(remoteAfter.Contains), "Original receipts retained unchanged.");
        if (phase is 11 or 12 or 13 or 15 or 16 or 17) ControlEvidence.Require(remoteBefore.SequenceEqual(remoteAfter), "Rejected/cached identity never rewrites or adds authority receipt.");
        if (phase == 10) ControlEvidence.Require(ControlEvidence.Item(after, ControlEvidence.Good).Location.Kind == LocationKind.ContainerSlot && ControlEvidence.Item(after, ControlEvidence.Good).Location.OwnerId == ControlEvidence.Vessel.Value, "Compatible recovery succeeds.");
    }
}
