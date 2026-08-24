using System.Collections.Immutable;
using Oxide.Core.Semantics.Contributions;
using Oxide.Core.Semantics.Declarations;
using Oxide.Core.Semantics.Diagnostics;
using Oxide.Core.Semantics.Identity;

namespace Oxide.Core.Semantics.Model;

public sealed record StrategicRegionEntity(
    EntityId Id,
    ContributionResolution<EntityId, StrategicRegionDeclaration> ContributionResolution,
    SemanticEntityStatus Status,
    EffectiveValue<string>? Name,
    ImmutableArray<EffectiveValue<int>> Provinces,
    ImmutableArray<SemanticDiagnostic> Diagnostics) : ISemanticEntity
{
    public ImmutableArray<StrategicRegionDeclaration> Contributions =>
        ContributionResolution.Contributions
            .Select(contribution => contribution.Contribution.Declaration)
            .ToImmutableArray();

    public StrategicRegionDeclaration? EffectiveDeclaration =>
        ContributionResolution.EffectiveContribution?.Declaration;
}
