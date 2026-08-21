using System.Collections.Immutable;
using Oxide.Core.Semantics.Declarations;
using Oxide.Core.Semantics.Diagnostics;
using Oxide.Core.Semantics.Identity;

namespace Oxide.Core.Semantics.Model;

public sealed record CountryEntity(
    EntityId Id,
    ImmutableArray<CountryTagDeclaration> Contributions,
    SemanticEntityStatus Status,
    EffectiveValue<string>? DefinitionPath,
    ImmutableArray<SemanticDiagnostic> Diagnostics) : ISemanticEntity;
