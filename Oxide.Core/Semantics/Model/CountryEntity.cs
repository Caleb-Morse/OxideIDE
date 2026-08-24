using System.Collections.Immutable;
using Oxide.Core.Semantics.Contributions;
using Oxide.Core.Semantics.Declarations;
using Oxide.Core.Semantics.Diagnostics;
using Oxide.Core.Semantics.Identity;

namespace Oxide.Core.Semantics.Model;

public sealed record CountryEntity(
    EntityId Id,
    ContributionResolution<EntityId, CountryTagDeclaration> ContributionResolution,
    SemanticEntityStatus Status,
    EffectiveValue<string>? DefinitionPath,
    ImmutableArray<SemanticDiagnostic> Diagnostics) : ISemanticEntity
{
    public ImmutableArray<CountryTagDeclaration> Contributions =>
        ContributionResolution.Contributions
            .Select(contribution => contribution.Contribution.Declaration)
            .ToImmutableArray();

    public CountryTagDeclaration? EffectiveDeclaration =>
        ContributionResolution.EffectiveContribution?.Declaration;
}
