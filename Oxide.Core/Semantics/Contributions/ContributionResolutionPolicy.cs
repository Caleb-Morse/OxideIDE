namespace Oxide.Core.Semantics.Contributions;

public sealed record ContributionResolutionPolicy(
    string Name,
    bool SelectHigherLayer)
{
    public static ContributionResolutionPolicy LayeredOverride { get; } =
        new("Layered override", SelectHigherLayer: true);

    public static ContributionResolutionPolicy Conservative { get; } =
        new("Conservative", SelectHigherLayer: false);
}
