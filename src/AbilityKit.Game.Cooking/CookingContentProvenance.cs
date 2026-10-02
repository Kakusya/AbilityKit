namespace AbilityKit.Game.Cooking;

public sealed record CookingContentSourceIdentity(string Path, string Sha256);

/// <summary>Source evidence is part of the validated configuration identity, not a second authority.</summary>
public sealed record CookingContentProvenance(
    string CatalogSchema,
    string CatalogSha256,
    IReadOnlyList<CookingContentSourceIdentity> Sources,
    IReadOnlyList<string> SelectedMenus)
{
    internal bool IsValid() => StringComparer.Ordinal.Equals(CatalogSchema, CookingMenuCatalog.CurrentSchema) && IsHash(CatalogSha256) &&
        Sources is { Count: > 0 } && SelectedMenus is { Count: > 0 } &&
        Sources.All(source => source is not null && !string.IsNullOrWhiteSpace(source.Path) && IsHash(source.Sha256)) &&
        Sources.Select(source => source.Path).Distinct(StringComparer.Ordinal).Count() == Sources.Count &&
        SelectedMenus.All(menu => !string.IsNullOrWhiteSpace(menu)) &&
        SelectedMenus.Distinct(StringComparer.Ordinal).Count() == SelectedMenus.Count;

    internal CookingContentProvenance Freeze() => new(CatalogSchema, CatalogSha256,
        Array.AsReadOnly(Sources.OrderBy(source => source.Path, StringComparer.Ordinal).ToArray()),
        Array.AsReadOnly(SelectedMenus.OrderBy(menu => menu, StringComparer.Ordinal).ToArray()));

    private static bool IsHash(string? hash) => hash is { Length: 64 } &&
        hash.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
