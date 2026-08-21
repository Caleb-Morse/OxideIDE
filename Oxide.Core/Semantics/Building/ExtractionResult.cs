using System.Collections.Immutable;
using Oxide.Core.Semantics.Diagnostics;

namespace Oxide.Core.Semantics.Building;

internal sealed record ExtractionResult<T>(
    ImmutableArray<T> Declarations,
    ImmutableArray<SemanticDiagnostic> Diagnostics);
