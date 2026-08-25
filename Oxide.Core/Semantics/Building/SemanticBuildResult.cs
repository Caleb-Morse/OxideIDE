using Oxide.Core.Semantics.Snapshots;
using System.Collections.Immutable;
using Oxide.Core.Workspaces.Refresh;

namespace Oxide.Core.Semantics.Building;

internal sealed record SemanticBuildResult(
    SemanticSnapshot Snapshot,
    double LocalisationIndexingMilliseconds,
    ImmutableArray<SemanticRefreshDomain> RebuiltDomains,
    ImmutableArray<SemanticRefreshDomain> ReusedDomains);
