using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AbilityKit.Game.Cooking.RichEvidence;
using AbilityKit.Game.Cooking.Session;
namespace AbilityKit.Game.Cooking.NetworkRichRecoveryAcceptance;
internal static class RichProof
{
    public static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    public static string Sha(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    public static string TextHash(string text) => Sha(Encoding.UTF8.GetBytes(text));
    public static RichCapture Capture(string id, CookingNetworkAuthorityCapture state) => new(id, System.Diagnostics.Stopwatch.GetTimestamp(), CookingNetworkWireCodec.Hash(state), TextHash(state.FullRecipe!.CanonicalText()), state.FullFront is null ? null : TextHash(state.FullFront.CanonicalText()), state);
    public static string Physical(CookingRecipeCheckpoint state) => JsonSerializer.Serialize(new { state.Items, state.Containers, state.Processes, state.NextProductId, state.NextProcessId, state.NextSettlementSequence, state.Settlements, state.ConsumedProducts, state.Supply, state.SupplyOrigins }, CookingNetworkWireCodec.JsonOptions);
    public static void Validate(CookingNetworkAuthorityCapture state)
    {
        var full = state.FullRecipe ?? throw new InvalidOperationException("Missing rich full recipe.");
        Require(full.SchemaVersion == 5 && state.FullFront is not null && state.InstalledLayout is not null && full.Supply is not null && full.Poses?.Count == 2, "Incomplete rich full baseline.");
        Require(full.Items.Select(x => x.Id).Distinct().Count() == full.Items.Count, "Copied item.");
        Require(full.Deduplication.Select(x => (x.Player, x.Command)).Distinct().Count() == full.Deduplication.Count, "Overwritten/colliding receipt.");
        foreach (var group in full.Items.Where(x => !x.Removed && x.Location.Kind is LocationKind.PlayerHand or LocationKind.WorldPosition or LocationKind.StationSlot).GroupBy(x => x.Location)) Require(group.Count() == 1, "Single-slot spatial/held ownership.");
        foreach (var c in full.Containers) Require(c.ItemIds.OrderBy(x => x.Value).SequenceEqual(full.Items.Where(x => !x.Removed && x.Location.Kind == LocationKind.ContainerSlot && x.Location.OwnerId == c.Id.Value).Select(x => x.Id).OrderBy(x => x.Value)), "Incomplete rich container.");
    }
    public static void Natural(CookingNetworkAuthorityCapture state)
    {
        Validate(state);
        Require(state.FullRecipe!.Settlements.Count == 2 && state.FullFront!.State.UnsatisfiedOrders.Count == 1 && state.Observation.Recipe!.Stars == 0 && state.Observation.Recipe.IsCompleted, "Natural 2 deliveries/1 unmet/0-star success.");
        Require(state.FullFront.State.Closing && state.FullFront.State.WashQueue.Count == 0 && state.FullFront.State.Tables.All(t => t.State == CookingFrontTableState.Free) && state.FullFront.State.Work.All(w => w.Player is null && w.Status != CookingFrontWorkStatus.Working), "Natural cleanup incomplete.");
    }
    public static void Retry(CookingRecipeCheckpoint before, CookingRecipeCheckpoint after, CookingNetworkWireResult original, CookingNetworkWireResult retry)
    {
        Require(retry.DomainCommandId == original.DomainCommandId && retry.Result is { Outcome: CookingRecipeOutcome.Accepted, IsDuplicate: true } && original.Result?.Outcome == CookingRecipeOutcome.Accepted && retry.Result.StateVersion == original.Result.StateVersion, "Real accepted cached Submit identity.");
        Require(Physical(before) == Physical(after) && before.Deduplication.SequenceEqual(after.Deduplication), "Submit retry mutated settlement/tombstones/allocator/history.");
    }
}
