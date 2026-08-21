namespace Oxide.Core.Semantics.Identity;

public readonly record struct EntityId(EntityKind Kind, string Namespace, string LocalKey)
{
    public static EntityId State(int stateId) => new(EntityKind.State, "global", stateId.ToString());

    public static EntityId Country(string tag) => new(EntityKind.Country, "tag", NormalizeCountryTag(tag));

    public static string NormalizeCountryTag(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        return tag.ToUpperInvariant();
    }

    public override string ToString() => $"{Kind}:{Namespace}:{LocalKey}";
}
