namespace Oxide.Core.Workspaces.Loading;

public sealed record WorkspaceLoadMetrics(
    int DocumentCount,
    int LoadedDocumentCount,
    int FailedDocumentCount,
    int WorkspaceDiagnosticCount,
    int SemanticDiagnosticCount,
    double DiscoveryMilliseconds,
    double DocumentLoadingMilliseconds,
    double ClausewitzDocumentLoadingMilliseconds,
    double LocalisationDocumentLoadingMilliseconds,
    double SemanticBuildingMilliseconds,
    double LocalisationIndexingMilliseconds,
    double TotalMilliseconds)
{
    public double DocumentsPerSecond => DocumentLoadingMilliseconds <= 0
        ? 0
        : DocumentCount / DocumentLoadingMilliseconds * 1_000;
}
