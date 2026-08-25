using System.Collections.Immutable;
using Oxide.Core.Semantics.Contributions;
using Oxide.Core.Semantics.Diagnostics;
using Oxide.Core.Semantics.Model;
using Oxide.Syntax.Diagnostics;

namespace Oxide.Core.Semantics.Resolution;

public sealed class ProvinceStrategicRegionIndex
{
    private readonly ImmutableDictionary<int, ImmutableArray<ProvinceStrategicRegionCandidate>> candidatesByProvince;

    internal ProvinceStrategicRegionIndex(
        ImmutableDictionary<int, StrategicRegionEntity> regions,
        ImmutableArray<SemanticDiagnostic>.Builder diagnostics)
    {
        candidatesByProvince = regions.Values
            .SelectMany(region => region.ContributionResolution.Contributions
                .Where(contribution => contribution.Disposition is ContributionDisposition.Effective
                    or ContributionDisposition.Ambiguous)
                .SelectMany(contribution => contribution.Contribution.Declaration.Provinces.Select(province =>
                    new ProvinceStrategicRegionCandidate(
                        province.Value,
                        region,
                        contribution.Contribution.Declaration,
                        province.Provenance))))
            .GroupBy(candidate => candidate.ProvinceId)
            .ToImmutableDictionary(
                group => group.Key,
                group => group
                    .OrderBy(candidate => candidate.Region.Id.LocalKey, StringComparer.Ordinal)
                    .ThenBy(candidate => candidate.Provenance.Layer.Position)
                    .ThenBy(candidate => candidate.Provenance.PhysicalPath, StringComparer.Ordinal)
                    .ThenBy(candidate => candidate.Provenance.Span.Start)
                    .ToImmutableArray());

        foreach (var entry in candidatesByProvince.OrderBy(entry => entry.Key))
        {
            if (!IsAmbiguous(entry.Value))
            {
                continue;
            }

            var hasAmbiguousIdentity = entry.Value.Any(candidate =>
                candidate.Region.Status is SemanticEntityStatus.Ambiguous);
            diagnostics.Add(new SemanticDiagnostic(
                "OXIDE4016",
                DiagnosticSeverity.Error,
                hasAmbiguousIdentity
                    ? $"Province {entry.Key} is claimed through an ambiguous strategic-region identity."
                    : $"Province {entry.Key} is claimed by several strategic regions.",
                null,
                entry.Value[0].Provenance,
                entry.Value.Skip(1).Select(candidate => candidate.Provenance).ToImmutableArray()));
        }
    }

    public bool IsEmpty => candidatesByProvince.Count == 0;

    public ImmutableDictionary<int, ImmutableArray<ProvinceStrategicRegionCandidate>> CandidatesByProvince =>
        candidatesByProvince;

    public ProvinceStrategicRegionResolution Resolve(int provinceId)
    {
        if (!candidatesByProvince.TryGetValue(provinceId, out var candidates))
        {
            return new MissingProvinceStrategicRegion(provinceId);
        }

        if (IsAmbiguous(candidates))
        {
            var reason = candidates.Any(candidate => candidate.Region.Status is SemanticEntityStatus.Ambiguous)
                ? "At least one candidate belongs to an ambiguous strategic-region identity."
                : "Several strategic regions claim the same province.";
            return new AmbiguousProvinceStrategicRegion(provinceId, candidates, reason);
        }

        return new ResolvedProvinceStrategicRegion(provinceId, candidates[0].Region, candidates);
    }

    private static bool IsAmbiguous(ImmutableArray<ProvinceStrategicRegionCandidate> candidates) =>
        candidates.Any(candidate => candidate.Region.Status is SemanticEntityStatus.Ambiguous)
        || candidates.Select(candidate => candidate.Region.Id).Distinct().Skip(1).Any();
}
