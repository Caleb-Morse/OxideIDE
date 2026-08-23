using System.Collections.Immutable;
using Oxide.Core.Semantics.Declarations;
using Oxide.Core.Semantics.Diagnostics;
using Oxide.Core.Semantics.Identity;

namespace Oxide.Core.Semantics.Model;

public sealed record StrategicRegionEntity(
    EntityId Id,
    ImmutableArray<StrategicRegionDeclaration> Contributions,
    SemanticEntityStatus Status,
    EffectiveValue<string>? Name,
    ImmutableArray<EffectiveValue<int>> Provinces,
    ImmutableArray<SemanticDiagnostic> Diagnostics) : ISemanticEntity;
