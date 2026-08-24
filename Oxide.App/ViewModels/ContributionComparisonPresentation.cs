using System.Collections.Immutable;

namespace Oxide.App.ViewModels;

public enum ContributionFieldDifference
{
    Unchanged,
    Changed,
    EffectiveOnly,
    ShadowedOnly,
}

public sealed record ContributionFieldComparisonPresentation(
    string FieldName,
    string EffectiveValue,
    string ShadowedValue,
    ContributionFieldDifference Difference,
    string DifferenceLabel);

public sealed record ContributionComparisonPresentation(
    string ShadowedContributionId,
    string ShadowedSummary,
    ContributionSourcePresentation ShadowedSource,
    ImmutableArray<ContributionFieldComparisonPresentation> Fields)
{
    public int DifferenceCount => Fields.Count(field => field.Difference is not ContributionFieldDifference.Unchanged);

    public string Summary => DifferenceCount == 1
        ? "1 structural difference"
        : $"{DifferenceCount:N0} structural differences";
}
