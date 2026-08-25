using System.Collections.Immutable;
using Oxide.Core.Workspaces.Loading;

namespace Oxide.Core.Workspaces.Refresh;

public sealed record WorkspaceRefreshResult(
    IncrementalRefreshRequest Request,
    WorkspaceRefreshOutcome Outcome,
    long PreviousSnapshotVersion,
    long? PublishedSnapshotVersion,
    WorkspaceRefreshMetrics Metrics,
    ImmutableArray<WorkspaceDiagnostic> Diagnostics);
