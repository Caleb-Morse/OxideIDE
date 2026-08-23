using Oxide.Core.Semantics.Snapshots;

namespace Oxide.Core.Semantics.Building;

internal sealed record SemanticBuildResult(
    SemanticSnapshot Snapshot,
    double LocalisationIndexingMilliseconds);
