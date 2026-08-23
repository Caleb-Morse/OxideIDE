using System.Collections.Immutable;
using Oxide.Core.Workspaces.Loading;

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
    LocalisationCorpusSummary Localisation,
    WorkspaceLoadMetrics WorkspacePerformance,
    double TotalLoadMilliseconds);
