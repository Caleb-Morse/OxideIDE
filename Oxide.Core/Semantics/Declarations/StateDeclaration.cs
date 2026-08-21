using System.Collections.Immutable;
using Oxide.Core.Semantics.Identity;
using Oxide.Core.Semantics.Model;
using Oxide.Core.Workspaces.Documents;

namespace Oxide.Core.Semantics.Declarations;

public sealed record StateDeclaration(
    DocumentId DocumentId,
    SourceProvenance Provenance,
    ImmutableArray<SourcedValue<int>> IdCandidates,
    ImmutableArray<SourcedValue<string>> NameCandidates,
    ImmutableArray<SourcedValue<long>> ManpowerCandidates,
    ImmutableArray<SourcedValue<string>> StateCategoryCandidates,
    ImmutableArray<NamedSourcedValue<decimal>> Resources,
    ImmutableArray<SourcedValue<int>> Provinces,
    ImmutableArray<SourcedValue<string>> OwnerCandidates,
    ImmutableArray<SourcedValue<string>> CoreTags)
{
    public EntityId? EntityId => IdCandidates.Length == 1
        ? Oxide.Core.Semantics.Identity.EntityId.State(IdCandidates[0].Value)
        : null;
}
