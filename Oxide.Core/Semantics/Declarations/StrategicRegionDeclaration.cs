using System.Collections.Immutable;
using Oxide.Core.Semantics.Identity;
using Oxide.Core.Semantics.Model;
using Oxide.Core.Workspaces.Documents;

namespace Oxide.Core.Semantics.Declarations;

public sealed record StrategicRegionDeclaration(
    DocumentId DocumentId,
    SourceProvenance Provenance,
    ImmutableArray<SourcedValue<int>> IdCandidates,
    bool HasSingleIdProperty,
    ImmutableArray<SourcedValue<string>> NameCandidates,
    ImmutableArray<SourcedValue<int>> Provinces)
{
    public EntityId? EntityId => HasSingleIdProperty && IdCandidates.Length == 1
        ? Oxide.Core.Semantics.Identity.EntityId.StrategicRegion(IdCandidates[0].Value)
        : null;
}
