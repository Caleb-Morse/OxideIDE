using System.Collections.Immutable;
using Oxide.Core.Semantics.Declarations;
using Oxide.Core.Semantics.Diagnostics;
using Oxide.Core.Semantics.Identity;
using Oxide.Core.Semantics.Resolution;

namespace Oxide.Core.Semantics.Model;

public sealed record StateEntity(
    EntityId Id,
    ImmutableArray<StateDeclaration> Contributions,
    SemanticEntityStatus Status,
    EffectiveValue<string>? Name,
    EffectiveValue<long>? Manpower,
    EffectiveValue<string>? StateCategory,
    ImmutableDictionary<string, EffectiveValue<decimal>> Resources,
    ImmutableArray<EffectiveValue<int>> Provinces,
    CountryReference? Owner,
    ImmutableArray<CountryReference> Cores,
    ImmutableArray<SemanticDiagnostic> Diagnostics) : ISemanticEntity;
