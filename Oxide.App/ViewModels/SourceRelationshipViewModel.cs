using System.Collections.Immutable;
using System.Globalization;
using Oxide.Core.Semantics.Identity;
using Oxide.Core.Workspaces.Snapshots;

namespace Oxide.App.ViewModels;

public sealed record SourceRelationshipViewModel(
    string Label,
    string Description,
    bool IsCurrent,
    SourceNavigationRequest NavigationRequest)
{
    public bool CanNavigate => !IsCurrent;

    public string AccessibleName => IsCurrent
        ? $"Current source: {Label}; {Description}"
        : $"Open related source: {Label}; {Description}";
}

internal static class SourceRelationshipProjector
{
    public static ImmutableArray<SourceRelationshipViewModel> Create(
        WorkspaceSnapshot snapshot,
        SourceNavigationRequest current)
    {
        var contribution = FindContribution(snapshot, current.SemanticIdentity);
        if (contribution is null)
        {
            return [];
        }

        return contribution.Contributions
            .Select(item => new SourceRelationshipViewModel(
                $"{item.DispositionLabel} · {item.Source.LayerName}",
                $"{item.Summary} · {item.Source.VirtualPath} · {item.Source.Location}",
                IsSameLocation(item.NavigationRequest, current),
                item.NavigationRequest))
            .ToImmutableArray();
    }

    public static bool TryRemap(
        WorkspaceSnapshot snapshot,
        SourceNavigationRequest previous,
        out SourceNavigationRequest remapped,
        out string failureReason)
    {
        var contribution = FindContribution(snapshot, previous.SemanticIdentity);
        if (contribution is null)
        {
            remapped = previous;
            failureReason = "The semantic identity no longer exists in the refreshed snapshot.";
            return false;
        }

        var sameSource = contribution.Contributions
            .Select(item => item.NavigationRequest)
            .Where(request => request.DocumentId == previous.DocumentId && request.LayerId == previous.LayerId)
            .ToArray();
        var exactSpan = sameSource.Where(request =>
            request.SpanStart == previous.SpanStart && request.SpanLength == previous.SpanLength).ToArray();
        if (exactSpan.Length == 1)
        {
            remapped = exactSpan[0];
            failureReason = string.Empty;
            return true;
        }

        if (sameSource.Length == 1)
        {
            remapped = sameSource[0];
            failureReason = string.Empty;
            return true;
        }

        remapped = previous;
        failureReason = sameSource.Length == 0
            ? "The exact document and layer no longer contribute to this identity."
            : "Multiple refreshed declarations match the previous document and layer.";
        return false;
    }

    private static ContributionSetPresentation? FindContribution(WorkspaceSnapshot snapshot, string semanticIdentity)
    {
        const string statePrefix = "State:global:";
        if (semanticIdentity.StartsWith(statePrefix, StringComparison.Ordinal) &&
            int.TryParse(semanticIdentity[statePrefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var stateId) &&
            snapshot.Semantics.States.TryGetValue(stateId, out var state))
        {
            return ContributionSetPresentation.Create(state, snapshot);
        }

        const string countryPrefix = "Country:tag:";
        if (semanticIdentity.StartsWith(countryPrefix, StringComparison.Ordinal) &&
            snapshot.Semantics.Countries.TryGetValue(semanticIdentity[countryPrefix.Length..], out var country))
        {
            return ContributionSetPresentation.Create(country, snapshot);
        }

        const string regionPrefix = "StrategicRegion:global:";
        if (semanticIdentity.StartsWith(regionPrefix, StringComparison.Ordinal) &&
            int.TryParse(semanticIdentity[regionPrefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var regionId) &&
            snapshot.Semantics.StrategicRegions.TryGetValue(regionId, out var region))
        {
            return ContributionSetPresentation.Create(region, snapshot);
        }

        const string localisationPrefix = "localisation:";
        if (semanticIdentity.StartsWith(localisationPrefix, StringComparison.Ordinal))
        {
            var value = semanticIdentity[localisationPrefix.Length..];
            var separator = value.IndexOf(':', StringComparison.Ordinal);
            if (separator > 0 && separator < value.Length - 1)
            {
                var identity = new LocalisationIdentity(
                    new LocalisationLanguage(value[..separator]),
                    new LocalisationKey(value[(separator + 1)..]));
                if (snapshot.Semantics.Localisations.TryGetValue(identity, out var entry))
                {
                    return ContributionSetPresentation.Create(entry, snapshot);
                }
            }
        }

        return null;
    }

    private static bool IsSameLocation(SourceNavigationRequest left, SourceNavigationRequest right) =>
        left.SnapshotVersion == right.SnapshotVersion &&
        left.DocumentId == right.DocumentId &&
        left.LayerId == right.LayerId &&
        left.SpanStart == right.SpanStart &&
        left.SpanLength == right.SpanLength;
}
