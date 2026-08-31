using System;
using System.IO;
using System.Linq;
using AbilityKit.Triggering.Runtime.Plan.Json;
using NUnit.Framework;

public sealed class MobaTriggerPlanAggregateTests
{
    private const string AbilityResourceDirectory =
        "Packages/com.abilitykit.demo.moba.view.runtime/Resources/ability";
    private const string SplitDirectory = AbilityResourceDirectory + "/triggers";
    private const string AggregatePath = AbilityResourceDirectory + "/ability_trigger_plans.json";

    [Test]
    public void Compile_SortsTriggersByIdDeterministically()
    {
        var documents = new[]
        {
            Source("z.json", 200),
            Source("a.json", 100)
        };

        var first = TriggerPlanAggregateCompiler.Compile(documents);
        var second = TriggerPlanAggregateCompiler.Compile(documents.Reverse());
        var database = new TriggerPlanJsonDatabase();
        database.LoadFromJson(first, "compiled-test-aggregate");
        var ids = database.Records.Select(record => record.TriggerId).ToArray();

        CollectionAssert.AreEqual(new[] { 100, 200 }, ids);
        Assert.AreEqual(first, second);
    }

    [Test]
    public void Compile_RejectsDuplicateTriggerIdsWithBothSourceNames()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            TriggerPlanAggregateCompiler.Compile(new[]
            {
                Source("first.json", 100),
                Source("second.json", 100)
            }));

        StringAssert.Contains("Duplicate trigger ID 100", exception.Message);
        StringAssert.Contains("first.json", exception.Message);
        StringAssert.Contains("second.json", exception.Message);
    }

    [Test]
    public void CheckedInAggregate_MatchesCanonicalSplitCompilation()
    {
        var documents = Directory.GetFiles(
                SplitDirectory,
                "*.json",
                SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => new TriggerPlanAggregateCompiler.SourceDocument(
                path.Substring(SplitDirectory.Length + 1).Replace('\\', '/'),
                File.ReadAllText(path)))
            .ToArray();

        Assert.IsNotEmpty(documents, "No maintained split trigger plan files were found.");

        var expected = TriggerPlanAggregateCompiler.Compile(documents);
        var actual = NormalizeNewlines(File.ReadAllText(AggregatePath));

        Assert.AreEqual(
            expected,
            actual,
            "ability_trigger_plans.json drifted from ability/triggers/**/*.json. "
            + "Run AbilityKit/Ability/Merge Trigger Plan JSON and commit the generated aggregate.");
    }

    private static TriggerPlanAggregateCompiler.SourceDocument Source(
        string name,
        int triggerId)
    {
        return new TriggerPlanAggregateCompiler.SourceDocument(
            name,
            "{\"triggers\":[{\"id\":" + triggerId
            + ",\"event\":\"\",\"enabled\":true,\"actions\":[]}]}" );
    }

    private static string NormalizeNewlines(string value)
    {
        return value.Replace("\r\n", "\n").TrimEnd('\n') + "\n";
    }
}
