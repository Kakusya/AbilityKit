using Xunit;

namespace AbilityKit.Game.Cooking.Tests;

// ET owns a process-wide World singleton. Tests constructing its real host share
// this collection; pure Cooking tests retain their normal parallel execution.
[CollectionDefinition(CookingEtHostTestCollection.Name)]
public sealed class CookingEtHostTestCollection
{
    public const string Name = "Cooking actual ET host";
}
