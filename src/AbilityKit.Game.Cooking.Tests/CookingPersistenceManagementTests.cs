using AbilityKit.Game.Cooking;
using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

[Trait("Gate", "CookingPersistenceManagement")]
public sealed class CookingPersistenceManagementTests
{
    private static readonly CookingProgressOwnerScope Owner = new(new CookingProgressOwnerId("fixture-owner"), new PlayerId("chef-a"));
    private static readonly CookingScope Match = new(new SessionId("persistence-session"), new WorldId("persistence-world"), new MatchId("persistence-match"));

    [Fact]
    public void P01_confirmed_settlement_mutates_only_long_term_progress_fields()
    {
        using var evidence = CreateEvidence("P01");
        var harness = CreateHarness();
        var result = Apply(harness, evidence, "P01", Settlement("settlement-a"),
            "confirmed settlement creates a revisioned long-term candidate without match runtime state");

        Assert.Equal(CookingSettlementDisposition.Applied, result.Disposition);
        Assert.Equal(1, harness.Persistence.Current.Revision);
        Assert.Equal(10, harness.Persistence.Current.Currency);
        Assert.Equal(3, harness.Persistence.Current.BusinessProgress);
        Assert.Equal(2, harness.Persistence.Current.Upgrades["oven"]);
        Assert.Contains("recipe-b", harness.Persistence.Current.Unlocks);
        Assert.Single(harness.Persistence.Current.AppliedSettlements);
        AssertEvidence(evidence.Path, "P01", 1);
    }

    [Fact]
    public void P02_same_identity_is_idempotent_and_conflicting_payload_is_rejected_without_mutation()
    {
        using var evidence = CreateEvidence("P02");
        var harness = CreateHarness();
        var settlement = Settlement("settlement-a");
        var first = Apply(harness, evidence, "P02", settlement, "first settlement creates one logical commit");
        var duplicate = Apply(harness, evidence, "P02", settlement, "same settlement identity returns idempotent result");
        var before = ProgressHash(harness.Persistence.Current);
        var conflict = Apply(harness, evidence, "P02", settlement with { Reward = Reward(currency: 99) },
            "same identity with different payload is rejected without extra progress mutation");

        Assert.Equal(CookingSettlementDisposition.Applied, first.Disposition);
        Assert.Equal(CookingSettlementDisposition.Duplicate, duplicate.Disposition);
        Assert.True(duplicate.IsDuplicate);
        Assert.Equal(CookingPersistenceReason.SettlementIdentityConflict, conflict.Reason);
        Assert.Equal(before, ProgressHash(harness.Persistence.Current));
        Assert.Equal(1, harness.Store.CommitCount);
        AssertEvidence(evidence.Path, "P02", 3);
    }

    [Fact]
    public void P03_distinct_settlement_identities_with_equal_rewards_remain_independently_auditable()
    {
        var harness = CreateHarness();
        var first = harness.Persistence.Apply(Settlement("settlement-a"));
        var second = harness.Persistence.Apply(Settlement("settlement-b"));

        Assert.Equal(CookingSettlementDisposition.Applied, first.Disposition);
        Assert.Equal(CookingSettlementDisposition.Applied, second.Disposition);
        Assert.Equal(20, harness.Persistence.Current.Currency);
        Assert.Equal(2, harness.Persistence.Current.AppliedSettlements.Count);
        Assert.Contains(new SettlementId("settlement-a"), harness.Persistence.Current.AppliedSettlements.Keys);
        Assert.Contains(new SettlementId("settlement-b"), harness.Persistence.Current.AppliedSettlements.Keys);
    }

    [Fact]
    public void P04_precommit_failure_leaves_no_ledger_or_progress_and_retry_commits_once()
    {
        using var evidence = CreateEvidence("P04");
        var harness = CreateHarness();
        harness.Store.NextFault = CookingPersistenceFault.CommitFailure;
        var failed = Apply(harness, evidence, "P04", Settlement("settlement-a"),
            "failed pre-commit leaves no partial settlement ledger or progress mutation");
        Assert.Equal(CookingPersistenceReason.StoreCommitFailed, failed.Reason);
        Assert.Empty(harness.Persistence.Current.AppliedSettlements);
        Assert.Equal(0, harness.Persistence.Current.Currency);
        Assert.Equal(0, harness.Store.CommitCount);

        var retry = Apply(harness, evidence, "P04", Settlement("settlement-a"),
            "retry after pre-commit failure creates exactly one combined commit");
        Assert.Equal(CookingSettlementDisposition.Applied, retry.Disposition);
        Assert.Single(harness.Persistence.Current.AppliedSettlements);
        Assert.Equal(10, harness.Persistence.Current.Currency);
        Assert.Equal(1, harness.Store.CommitCount);
        AssertEvidence(evidence.Path, "P04", 2);
    }

    [Fact]
    public void P05_postcommit_response_loss_restarts_and_retries_without_duplicate_reward()
    {
        using var evidence = CreateEvidence("P05");
        var harness = CreateHarness();
        harness.Store.NextFault = CookingPersistenceFault.ResponseLostAfterCommit;
        var uncertain = Apply(harness, evidence, "P05", Settlement("settlement-a"),
            "post-commit response loss is distinct from an uncommitted failure");
        Assert.Equal(CookingPersistenceReason.ResponseLostAfterCommit, uncertain.Reason);
        Assert.Equal(10, harness.Persistence.Current.Currency);

        var restarted = new CookingProgressPersistence(Owner, Config(), InitialProgress(), harness.Store);
        var restored = restarted.RestartReadBack();
        Assert.True(restored.Accepted);
        var retry = restarted.Apply(Settlement("settlement-a"));
        Assert.Equal(CookingSettlementDisposition.Duplicate, retry.Disposition);
        Assert.Equal(10, restarted.Current.Currency);
        Assert.Equal(1, harness.Store.CommitCount);
        AssertEvidence(evidence.Path, "P05", 1);
    }

    [Fact]
    public void P06_interrupted_write_never_becomes_visible_to_readers()
    {
        var harness = CreateHarness();
        Assert.Equal(CookingSettlementDisposition.Applied, harness.Persistence.Apply(Settlement("settlement-a")).Disposition);
        var firstRevision = harness.Persistence.Current.Revision;
        harness.Store.NextFault = CookingPersistenceFault.CommitFailure;
        Assert.Equal(CookingPersistenceReason.StoreCommitFailed, harness.Persistence.Apply(Settlement("settlement-b")).Reason);

        var restarted = new CookingProgressPersistence(Owner, Config(), InitialProgress(), harness.Store);
        var read = restarted.RestartReadBack();
        Assert.True(read.Accepted);
        Assert.Equal(firstRevision, restarted.Current.Revision);
        Assert.Equal(10, restarted.Current.Currency);
        Assert.Single(restarted.Current.AppliedSettlements);
    }

    [Fact]
    public void P07_duplicate_revision_readback_is_stable_without_another_logical_commit()
    {
        var harness = CreateHarness();
        Assert.Equal(CookingSettlementDisposition.Applied, harness.Persistence.Apply(Settlement("settlement-a")).Disposition);
        var first = harness.Persistence.RestartReadBack();
        var second = harness.Persistence.RestartReadBack();

        Assert.True(first.Accepted);
        Assert.True(second.Accepted);
        Assert.Equal(1, harness.Store.CommitCount);
        Assert.Equal(1, harness.Persistence.Current.Revision);
    }

    [Fact]
    public void P08_restart_restores_validated_long_term_progress_only()
    {
        var harness = CreateHarness();
        Assert.Equal(CookingSettlementDisposition.Applied, harness.Persistence.Apply(Settlement("settlement-a")).Disposition);
        var restarted = new CookingProgressPersistence(Owner, Config(), InitialProgress(), harness.Store);

        var result = restarted.RestartReadBack();

        Assert.True(result.Accepted);
        Assert.Equal(10, restarted.Current.Currency);
        Assert.Equal(3, restarted.Current.BusinessProgress);
        Assert.Equal(1, restarted.Current.Revision);
        Assert.DoesNotContain("persistence-match", CookingProgressCodec.Serialize(result.Envelope!));
    }

    [Fact]
    public void P09_corrupt_unknown_version_owner_and_config_mismatch_preserve_valid_current_progress()
    {
        var harness = CreateHarness();
        Assert.Equal(CookingSettlementDisposition.Applied, harness.Persistence.Apply(Settlement("settlement-a")).Disposition);
        var before = ProgressHash(harness.Persistence.Current);

        harness.Store.NextFault = CookingPersistenceFault.TamperedRead;
        var tampered = harness.Persistence.RestartReadBack();
        Assert.False(tampered.Accepted);
        Assert.Equal(CookingPersistenceReason.IntegrityFailure, tampered.Reason);
        Assert.Equal(before, ProgressHash(harness.Persistence.Current));

        var unknown = CookingProgressCodec.Deserialize(CookingProgressCodec.Serialize(
            CookingProgressCodec.CreateEnvelope(harness.Persistence.Current) with { FormatVersion = 99 }));
        Assert.False(unknown.Accepted);
        Assert.Equal(CookingPersistenceReason.UnknownFormatVersion, unknown.Reason);

        var foreign = new CookingProgressPersistence(new CookingProgressOwnerScope(new CookingProgressOwnerId("other"), new PlayerId("chef-a")),
            Config(), InitialProgress(new CookingProgressOwnerScope(new CookingProgressOwnerId("other"), new PlayerId("chef-a"))), harness.Store);
        var foreignRead = foreign.RestartReadBack();
        Assert.False(foreignRead.Accepted);
        Assert.Equal(CookingPersistenceReason.OwnerMismatch, foreignRead.Reason);

        var incompatibleConfig = Config() with { Sha256 = new string('A', 64) };
        var incompatible = new CookingProgressPersistence(Owner, incompatibleConfig, InitialProgress(config: incompatibleConfig), harness.Store);
        var incompatibleRead = incompatible.RestartReadBack();
        Assert.False(incompatibleRead.Accepted);
        Assert.Equal(CookingPersistenceReason.ConfigIdentityMismatch, incompatibleRead.Reason);
    }

    [Fact]
    public void codec_rejects_malformed_and_oversized_records_without_throwing()
    {
        var malformed = CookingProgressCodec.Deserialize("{\"formatVersion\":1,\"progress\":null,\"integritySha256\":\"x\"}");
        var oversized = CookingProgressCodec.Deserialize(new string('x', CookingProgressCodec.MaximumRecordCharacters + 1));

        Assert.False(malformed.Accepted);
        Assert.Equal(CookingPersistenceReason.RecordTruncated, malformed.Reason);
        Assert.False(oversized.Accepted);
        Assert.Equal(CookingPersistenceReason.RecordTooLarge, oversized.Reason);
    }

    [Fact]
    public void current_progress_is_frozen_and_caller_mutation_cannot_bypass_commit()
    {
        var harness = CreateHarness();
        Assert.Equal(CookingSettlementDisposition.Applied, harness.Persistence.Apply(Settlement("settlement-a")).Disposition);
        var exposed = harness.Persistence.Current;

        Assert.False(exposed.Upgrades is Dictionary<string, int>);
        Assert.False(exposed.AppliedSettlements is Dictionary<SettlementId, CookingAppliedSettlement>);
        Assert.Equal(10, harness.Persistence.Current.Currency);
        Assert.Equal(1, harness.Store.CommitCount);
    }

    [Fact]
    public void P10_owner_save_and_exit_policies_remain_blocked_without_default_behavior()
    {
        var harness = CreateHarness();
        var before = ProgressHash(harness.Persistence.Current);

        var result = harness.Persistence.RequireOwnerDecision(CookingOwnerDecision.SaveAndSettlement);

        Assert.Equal(CookingSettlementDisposition.Rejected, result.Disposition);
        Assert.Equal(CookingPersistenceReason.BlockedByOwnerDecision, result.Reason);
        Assert.Equal(before, ProgressHash(harness.Persistence.Current));
    }

    private static Harness CreateHarness()
    {
        var store = new InMemoryCookingProgressStore();
        var persistence = new CookingProgressPersistence(Owner, Config(), InitialProgress(), store);
        return new Harness(persistence, store);
    }

    private static CookingConfigurationIdentity Config() => new(CookingConfigurationIdentity.CurrentSchema, new string('F', 64));

    private static CookingLongTermProgress InitialProgress(CookingProgressOwnerScope? owner = null,
        CookingConfigurationIdentity? config = null) => new(
        owner ?? Owner, (config ?? Config()).ToString(), 1, 0, Array.Empty<string>(), new Dictionary<string, int>(), 0, 0,
        new Dictionary<SettlementId, CookingAppliedSettlement>());

    private static CookingConfirmedSettlement Settlement(string id) => new(
        Owner, Match, new SettlementId(id), true, 1, Config(), Reward());

    private static CookingProgressReward Reward(long currency = 10) => new(
        new[] { "recipe-b" }, new Dictionary<string, int>(StringComparer.Ordinal) { ["oven"] = 2 }, currency, 3);

    private static CookingSettlementResult Apply(Harness harness, EvidenceScope evidence, string testId,
        CookingConfirmedSettlement settlement, string summary)
    {
        var before = harness.Persistence.Current;
        var result = harness.Persistence.Apply(settlement);
        CookingPersistenceAcceptanceEvidenceWriter.Append(evidence.Path, new CookingPersistenceAcceptanceEvidence(
            testId, "apply", $"{Owner.Owner}/{Owner.Player}", settlement.Id.Value, ProgressHash(before),
            ProgressHash(harness.Persistence.Current), before.Revision, harness.Persistence.Current.Revision,
            result.Disposition.ToString(), result.Reason.ToString(), summary, "dotnet test AbilityKit.Game.Cooking.Tests",
            DateTimeOffset.UtcNow.ToString("O")));
        if (result.Disposition is CookingSettlementDisposition.Rejected or CookingSettlementDisposition.Failed)
            Assert.Equal(ProgressHash(before), ProgressHash(harness.Persistence.Current));
        return result;
    }

    private static string ProgressHash(CookingLongTermProgress progress) =>
        CookingProgressCodec.CreateEnvelope(progress).IntegritySha256;

    private static void AssertEvidence(string path, string testId, int count)
    {
        var records = CookingPersistenceAcceptanceEvidenceWriter.ReadAll(path);
        Assert.Equal(count, records.Count);
        Assert.All(records, record => Assert.Equal(testId, record.TestId));
    }

    private sealed record Harness(CookingProgressPersistence Persistence, InMemoryCookingProgressStore Store);

    private sealed class EvidenceScope : IDisposable
    {
        private readonly string _directory;
        private readonly bool _keepArtifacts;

        public EvidenceScope(string testId)
        {
            var requestedRoot = Environment.GetEnvironmentVariable("COOKING_PERSISTENCE_EVIDENCE_DIRECTORY");
            _keepArtifacts = !string.IsNullOrWhiteSpace(requestedRoot);
            var root = _keepArtifacts ? System.IO.Path.GetFullPath(requestedRoot!) :
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AbilityKit.Game.Cooking.Tests", "persistence");
            _directory = System.IO.Path.Combine(root, testId, Guid.NewGuid().ToString("N"));
            Path = System.IO.Path.Combine(_directory, "persistence-management.jsonl");
        }

        public string Path { get; }

        public void Dispose()
        {
            if (!_keepArtifacts && Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }
    }

    private static EvidenceScope CreateEvidence(string testId) => new(testId);
}
