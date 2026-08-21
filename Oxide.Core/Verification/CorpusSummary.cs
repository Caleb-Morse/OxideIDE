using System.Collections.Immutable;

namespace Oxide.Core.Verification;

public sealed record CorpusSummary(
    string WorkspaceName,
    long SnapshotVersion,
    int FilesDiscovered,
    int DocumentsLoaded,
    int DocumentsFailed,
    int SyntaxDiagnosticCount,
    ImmutableSortedDictionary<string, int> SyntaxDiagnosticsByCode,
    ImmutableSortedDictionary<string, int> WorkspaceDiagnosticsByCode,
    int StateDeclarationCount,
    int StateEntityCount,
    int CountryDeclarationCount,
    int CountryEntityCount,
    int SemanticDiagnosticCount,
    ImmutableSortedDictionary<string, int> SemanticDiagnosticsByCode,
    ReferenceResolutionCounts CountryReferences,
    double TotalLoadMilliseconds);
