using System.Collections.Immutable;
using Oxide.Core.Semantics.Contributions;
using Oxide.Core.Semantics.Declarations;
using Oxide.Core.Semantics.Identity;
using Oxide.Core.Semantics.Model;
using Oxide.Core.Workspaces.Documents;
using Oxide.Core.Workspaces.Snapshots;

namespace Oxide.App.ViewModels;

public enum ContributionOutcomePresentation
{
    Effective,
    Ambiguous,
    Invalid,
    Missing,
    Unsupported,
}

public sealed record ContributionSourcePresentation(
    long SnapshotVersion,
    DocumentId DocumentId,
    string PhysicalPath,
    string VirtualPath,
    string LayerId,
    string LayerName,
    string LayerKind,
    int LayerPosition,
    int SpanStart,
    int SpanLength,
    string Location);

public sealed record ContributionItemPresentation(
    string ContributionId,
    string Summary,
    ContributionDisposition Disposition,
    string DispositionLabel,
    string Explanation,
    ContributionSourcePresentation Source,
    SourceNavigationRequest NavigationRequest)
{
    public bool IsEffective => Disposition is ContributionDisposition.Effective;

    public string AccessibleName =>
        $"{DispositionLabel} contribution: {Summary}; {Source.LayerName}; {Source.Location}";

    public string OpenSourceAccessibleName => $"Open source for {DispositionLabel.ToLowerInvariant()} contribution {Summary}";
}

public sealed record ContributionSetPresentation(
    string SemanticIdentity,
    ContributionOutcomePresentation Outcome,
    string OutcomeLabel,
    string ResolutionReason,
    ContributionSourcePresentation? EffectiveSource,
    ImmutableArray<ContributionItemPresentation> Contributions,
    ImmutableArray<ContributionComparisonPresentation> Comparisons)
{
    public int ContributionCount => Contributions.Length;

    public bool HasCompetingContributions => Contributions.Length > 1;

    public bool HasComparisons => Comparisons.Length > 0;

    public SourceNavigationRequest? EffectiveNavigationRequest =>
        Contributions.FirstOrDefault(contribution => contribution.IsEffective)?.NavigationRequest;

    public bool HasEffectiveSource => EffectiveNavigationRequest is not null;

    public string ContributionCountLabel => ContributionCount == 1
        ? "1 contribution"
        : $"{ContributionCount:N0} contributions";

    public string EffectiveLayerLabel => EffectiveSource is null
        ? "No effective layer"
        : $"Effective from {EffectiveSource.LayerName}";

    public static ContributionSetPresentation Create(StateEntity state, WorkspaceSnapshot snapshot) =>
        Create(
            state.ContributionResolution,
            snapshot,
            state.Id.ToString(),
            declaration => $"State {declaration.IdCandidates[0].Value}",
            CompareStates);

    public static ContributionSetPresentation Create(CountryEntity country, WorkspaceSnapshot snapshot) =>
        Create(
            country.ContributionResolution,
            snapshot,
            country.Id.ToString(),
            declaration => $"{declaration.NormalizedTag} → {declaration.DefinitionPath.Value}",
            CompareCountries);

    public static ContributionSetPresentation Create(StrategicRegionEntity region, WorkspaceSnapshot snapshot) =>
        Create(
            region.ContributionResolution,
            snapshot,
            region.Id.ToString(),
            declaration => $"Strategic region {declaration.IdCandidates[0].Value}",
            CompareStrategicRegions);

    public static ContributionSetPresentation Create(LocalisationEntry entry, WorkspaceSnapshot snapshot) =>
        Create(
            entry.Resolution,
            snapshot,
            $"localisation:{entry.Identity.Language.Value}:{entry.Identity.Key.Value}",
            declaration => $"{declaration.Key.Value}: {declaration.Value.Value}",
            CompareLocalisations);

    private static ContributionSetPresentation Create<TIdentity, TDeclaration>(
        ContributionResolution<TIdentity, TDeclaration> resolution,
        WorkspaceSnapshot snapshot,
        string semanticIdentity,
        Func<TDeclaration, string> describe,
        Func<TDeclaration, TDeclaration, ImmutableArray<ContributionFieldComparisonPresentation>> compare)
        where TIdentity : notnull
    {
        var contributions = resolution.Contributions
            .Select(contribution =>
            {
                var source = CreateSource(contribution.Contribution.Provenance, snapshot);
                return new ContributionItemPresentation(
                    contribution.Contribution.Id.Value,
                    describe(contribution.Contribution.Declaration),
                    contribution.Disposition,
                    DescribeDisposition(contribution.Disposition),
                    contribution.Explanation,
                    source,
                    CreateNavigationRequest(source, semanticIdentity));
            })
            .ToImmutableArray();
        var outcome = resolution.Kind switch
        {
            ContributionResolutionKind.Effective => ContributionOutcomePresentation.Effective,
            ContributionResolutionKind.DuplicateWithinLayer or ContributionResolutionKind.Ambiguous =>
                ContributionOutcomePresentation.Ambiguous,
            ContributionResolutionKind.InvalidWinner => ContributionOutcomePresentation.Invalid,
            ContributionResolutionKind.Missing => ContributionOutcomePresentation.Missing,
            _ => ContributionOutcomePresentation.Unsupported,
        };
        var comparisons = resolution.EffectiveContribution is not { } effective
            ? []
            : resolution.ShadowedContributions
                .Select(shadowed =>
                {
                    var source = CreateSource(shadowed.Contribution.Provenance, snapshot);
                    return new ContributionComparisonPresentation(
                        shadowed.Contribution.Id.Value,
                        describe(shadowed.Contribution.Declaration),
                        source,
                        CreateNavigationRequest(source, semanticIdentity),
                        compare(effective.Declaration, shadowed.Contribution.Declaration));
                })
                .ToImmutableArray();

        return new ContributionSetPresentation(
            semanticIdentity,
            outcome,
            DescribeOutcome(outcome),
            resolution.Reason.Explanation,
            contributions.FirstOrDefault(contribution => contribution.IsEffective)?.Source,
            contributions,
            comparisons);
    }

    private static ImmutableArray<ContributionFieldComparisonPresentation> CompareStates(
        StateDeclaration effective,
        StateDeclaration shadowed) =>
        [
            Field("Name key", Values(effective.NameCandidates), Values(shadowed.NameCandidates)),
            Field("Manpower", Values(effective.ManpowerCandidates), Values(shadowed.ManpowerCandidates)),
            Field("Category", Values(effective.StateCategoryCandidates), Values(shadowed.StateCategoryCandidates)),
            Field("Provinces", Values(effective.Provinces), Values(shadowed.Provinces)),
            Field("Resources", NamedValues(effective.Resources), NamedValues(shadowed.Resources)),
            Field("Owner", Values(effective.OwnerCandidates), Values(shadowed.OwnerCandidates)),
            Field("Cores", Values(effective.CoreTags), Values(shadowed.CoreTags)),
        ];

    private static ImmutableArray<ContributionFieldComparisonPresentation> CompareCountries(
        CountryTagDeclaration effective,
        CountryTagDeclaration shadowed) =>
        [
            Field("Tag", effective.NormalizedTag, shadowed.NormalizedTag),
            Field("History path", effective.DefinitionPath.Value, shadowed.DefinitionPath.Value),
        ];

    private static ImmutableArray<ContributionFieldComparisonPresentation> CompareStrategicRegions(
        StrategicRegionDeclaration effective,
        StrategicRegionDeclaration shadowed) =>
        [
            Field("Name key", Values(effective.NameCandidates), Values(shadowed.NameCandidates)),
            Field("Provinces", Values(effective.Provinces), Values(shadowed.Provinces)),
        ];

    private static ImmutableArray<ContributionFieldComparisonPresentation> CompareLocalisations(
        LocalisationDeclaration effective,
        LocalisationDeclaration shadowed) =>
        [
            Field("Language", effective.Language.Value, shadowed.Language.Value),
            Field("Key", effective.Key.Value, shadowed.Key.Value),
            Field("Version", effective.Version?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                shadowed.Version?.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Field("Text", effective.Value.Value, shadowed.Value.Value),
        ];

    private static ContributionFieldComparisonPresentation Field(
        string name,
        string? effective,
        string? shadowed)
    {
        var difference = (effective, shadowed) switch
        {
            (null, not null) => ContributionFieldDifference.ShadowedOnly,
            (not null, null) => ContributionFieldDifference.EffectiveOnly,
            _ when string.Equals(effective, shadowed, StringComparison.Ordinal) =>
                ContributionFieldDifference.Unchanged,
            _ => ContributionFieldDifference.Changed,
        };
        return new ContributionFieldComparisonPresentation(
            name,
            effective ?? "Not declared",
            shadowed ?? "Not declared",
            difference,
            difference switch
            {
                ContributionFieldDifference.Unchanged => "Unchanged",
                ContributionFieldDifference.Changed => "Changed",
                ContributionFieldDifference.EffectiveOnly => "Effective only",
                ContributionFieldDifference.ShadowedOnly => "Shadowed only",
                _ => difference.ToString(),
            });
    }

    private static string? Values<T>(IEnumerable<SourcedValue<T>> values)
    {
        var array = values.Select(value => Convert.ToString(value.Value,
            System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        return array.Length == 0 ? null : string.Join(", ", array);
    }

    private static string? NamedValues<T>(IEnumerable<NamedSourcedValue<T>> values)
    {
        var array = values
            .OrderBy(value => value.Name, StringComparer.Ordinal)
            .Select(value => $"{value.Name}={Convert.ToString(value.Value.Value, System.Globalization.CultureInfo.InvariantCulture)}")
            .ToArray();
        return array.Length == 0 ? null : string.Join(", ", array);
    }

    private static ContributionSourcePresentation CreateSource(
        SourceProvenance provenance,
        WorkspaceSnapshot snapshot)
    {
        var virtualPath = snapshot.DocumentsById.TryGetValue(provenance.DocumentId, out var document)
            ? document.VirtualPath.Value
            : "Unknown virtual path";
        var location = document?.Text is null
            ? $"Offset {provenance.Span.Start}"
            : DescribeLocation(document, provenance.Span.Start);
        return new ContributionSourcePresentation(
            snapshot.Version,
            provenance.DocumentId,
            provenance.PhysicalPath,
            virtualPath,
            provenance.Layer.Id.Value,
            provenance.Layer.DisplayName,
            provenance.Layer.Kind.ToString(),
            provenance.Layer.Position,
            provenance.Span.Start,
            provenance.Span.Length,
            location);
    }

    private static string DescribeLocation(SourceDocument document, int offset)
    {
        var position = document.Text!.GetPosition(offset);
        return $"Line {position.Line + 1}, column {position.Character + 1}";
    }

    private static SourceNavigationRequest CreateNavigationRequest(
        ContributionSourcePresentation source,
        string semanticIdentity) =>
        new(
            source.SnapshotVersion,
            source.DocumentId,
            source.PhysicalPath,
            source.VirtualPath,
            source.LayerId,
            source.LayerName,
            source.SpanStart,
            source.SpanLength,
            semanticIdentity,
            source.Location,
            $"Open the source contribution for {semanticIdentity}");

    private static string DescribeDisposition(ContributionDisposition disposition) => disposition switch
    {
        ContributionDisposition.Effective => "Effective",
        ContributionDisposition.Shadowed => "Shadowed",
        ContributionDisposition.Ambiguous => "Ambiguous",
        ContributionDisposition.Invalid => "Invalid",
        ContributionDisposition.Excluded => "Excluded",
        _ => disposition.ToString(),
    };

    private static string DescribeOutcome(ContributionOutcomePresentation outcome) => outcome switch
    {
        ContributionOutcomePresentation.Effective => "Effective contribution selected",
        ContributionOutcomePresentation.Ambiguous => "No contribution selected because the outcome is ambiguous",
        ContributionOutcomePresentation.Invalid => "The highest-precedence contribution is invalid",
        ContributionOutcomePresentation.Missing => "No eligible contribution is available",
        ContributionOutcomePresentation.Unsupported => "The contribution outcome is unsupported",
        _ => outcome.ToString(),
    };
}
