using System.Collections.Immutable;
using Oxide.Core.Semantics.Model;
using Oxide.Core.Semantics.Resolution;
using Oxide.Core.Workspaces.Snapshots;

namespace Oxide.App.ViewModels;

public sealed record ProvinceRegionEvidenceViewModel(
    int ProvinceId,
    string Outcome,
    string StateSource,
    string RegionSources);

internal sealed record StrategicRegionMembershipPresentation(
    string DisplayName,
    string Status,
    string Summary,
    string SearchText,
    ImmutableArray<ProvinceRegionEvidenceViewModel> Evidence,
    int DiagnosticCount)
{
    public static StrategicRegionMembershipPresentation Create(
        StateStrategicRegionMembership membership,
        WorkspaceSnapshot snapshot,
        string language,
        bool allowEnglishFallback)
    {
        var regions = membership.Provinces
            .SelectMany(reference => reference.Resolution switch
            {
                ResolvedProvinceStrategicRegion resolved => [resolved.Region],
                AmbiguousProvinceStrategicRegion ambiguous => ambiguous.Candidates.Select(candidate => candidate.Region),
                _ => [],
            })
            .DistinctBy(region => region.Id)
            .OrderBy(region => ParseId(region))
            .ToImmutableArray();
        var labels = regions.ToImmutableDictionary(
            region => region.Id,
            region => DescribeRegion(region, snapshot, language, allowEnglishFallback));
        var displayName = membership.Status switch
        {
            StateStrategicRegionMembershipStatus.SingleRegion or StateStrategicRegionMembershipStatus.Partial =>
                regions.Select(region => labels[region.Id]).FirstOrDefault() ?? "No strategic region",
            StateStrategicRegionMembershipStatus.Split =>
                string.Join(", ", regions.Select(region => labels[region.Id])),
            StateStrategicRegionMembershipStatus.Ambiguous => "Ambiguous strategic-region membership",
            StateStrategicRegionMembershipStatus.NoProvinces => "No provinces to resolve",
            _ => "No strategic region",
        };
        var status = membership.Status switch
        {
            StateStrategicRegionMembershipStatus.SingleRegion => "Single region",
            StateStrategicRegionMembershipStatus.Split => "Split across regions",
            StateStrategicRegionMembershipStatus.Partial => "Partial coverage",
            StateStrategicRegionMembershipStatus.Missing => "Missing membership",
            StateStrategicRegionMembershipStatus.Ambiguous => "Ambiguous membership",
            StateStrategicRegionMembershipStatus.NoProvinces => "No provinces",
            _ => membership.Status.ToString(),
        };
        var summary = DescribeCoverage(membership);
        var evidence = membership.Provinces
            .Select(reference => CreateEvidence(reference, labels, snapshot))
            .ToImmutableArray();
        return new StrategicRegionMembershipPresentation(
            displayName,
            status,
            summary,
            $"{displayName} {status} {summary} {string.Join(' ', regions.Select(region => labels[region.Id]))}",
            evidence,
            membership.Diagnostics.Length);
    }

    private static ProvinceRegionEvidenceViewModel CreateEvidence(
        ProvinceStrategicRegionReference reference,
        ImmutableDictionary<Oxide.Core.Semantics.Identity.EntityId, string> labels,
        WorkspaceSnapshot snapshot)
    {
        var outcome = reference.Resolution switch
        {
            ResolvedProvinceStrategicRegion resolved => $"Resolved to {labels[resolved.Region.Id]}",
            AmbiguousProvinceStrategicRegion ambiguous =>
                $"Ambiguous: {string.Join(", ", ambiguous.Candidates
                    .Select(candidate => labels[candidate.Region.Id]).Distinct(StringComparer.Ordinal))}",
            MissingProvinceStrategicRegion => "No strategic-region claim",
            _ => "Unresolved",
        };
        var regionSources = reference.Resolution switch
        {
            ResolvedProvinceStrategicRegion resolved => DescribeCandidates(resolved.Candidates, labels, snapshot),
            AmbiguousProvinceStrategicRegion ambiguous => DescribeCandidates(ambiguous.Candidates, labels, snapshot),
            _ => "No region-side source",
        };
        return new ProvinceRegionEvidenceViewModel(
            reference.ProvinceId,
            outcome,
            DescribeProvenance(reference.StateProvince.Provenance, snapshot),
            regionSources);
    }

    private static string DescribeCandidates(
        ImmutableArray<ProvinceStrategicRegionCandidate> candidates,
        ImmutableDictionary<Oxide.Core.Semantics.Identity.EntityId, string> labels,
        WorkspaceSnapshot snapshot) =>
        string.Join(Environment.NewLine, candidates.Select(candidate =>
            $"{labels[candidate.Region.Id]} — {DescribeProvenance(candidate.Provenance, snapshot)}"));

    private static string DescribeRegion(
        StrategicRegionEntity region,
        WorkspaceSnapshot snapshot,
        string language,
        bool allowEnglishFallback)
    {
        var id = ParseId(region);
        var name = snapshot.Semantics.LocalisationResolver.ResolveName(region, language, allowEnglishFallback);
        return name.DisplayText == $"Strategic region {id}"
            ? name.DisplayText
            : $"{name.DisplayText} · Region {id}";
    }

    private static string DescribeCoverage(StateStrategicRegionMembership membership)
    {
        var total = membership.Provinces.Length;
        return membership.Status switch
        {
            StateStrategicRegionMembershipStatus.NoProvinces => "The state declares no valid provinces.",
            StateStrategicRegionMembershipStatus.SingleRegion =>
                $"All {total:N0} provinces resolve to one region.",
            StateStrategicRegionMembershipStatus.Partial =>
                $"{membership.ResolvedProvinceCount:N0} of {total:N0} provinces resolved; {membership.MissingProvinceCount:N0} missing.",
            StateStrategicRegionMembershipStatus.Split =>
                $"{membership.ResolvedProvinceCount:N0} of {total:N0} provinces resolve across {membership.Regions.Length:N0} regions.",
            StateStrategicRegionMembershipStatus.Ambiguous =>
                $"{membership.AmbiguousProvinceCount:N0} of {total:N0} provinces have competing claims.",
            _ => $"None of the state's {total:N0} provinces have a region claim.",
        };
    }

    private static string DescribeProvenance(SourceProvenance provenance, WorkspaceSnapshot snapshot)
    {
        if (!snapshot.DocumentsById.TryGetValue(provenance.DocumentId, out var document) || document.Text is null)
        {
            return $"{provenance.PhysicalPath}, offset {provenance.Span.Start} ({provenance.Layer.Kind})";
        }

        var position = document.Text.GetPosition(provenance.Span.Start);
        return $"{provenance.PhysicalPath}, line {position.Line + 1}, column {position.Character + 1} ({provenance.Layer.Kind})";
    }

    private static int ParseId(StrategicRegionEntity region) =>
        int.Parse(region.Id.LocalKey, System.Globalization.CultureInfo.InvariantCulture);
}
