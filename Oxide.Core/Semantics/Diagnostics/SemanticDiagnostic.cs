using System.Collections.Immutable;
using Oxide.Core.Semantics.Identity;
using Oxide.Core.Semantics.Model;
using Oxide.Syntax.Diagnostics;

namespace Oxide.Core.Semantics.Diagnostics;

public sealed record SemanticDiagnostic(
    string Code,
    DiagnosticSeverity Severity,
    string Message,
    EntityId? EntityId,
    SourceProvenance? Provenance,
    ImmutableArray<SourceProvenance> RelatedProvenance);
