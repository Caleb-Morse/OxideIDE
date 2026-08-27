# Workspace core

## Scope

`Oxide.Core.Workspaces` opens a base-game root and an optional active-mod root,
discovers the currently supported files, parses them away from the UI thread,
and publishes an immutable `WorkspaceSnapshot`.

The discovery profile includes:

- `history/states/*.txt`;
- `map/strategicregions/*.txt`;
- `common/country_tags/*.txt`; and
- `localisation/**/*.yml`.

Additional directories belong in later schema/profile work rather than being
silently treated as supported.

## Configuration and content layers

`WorkspaceConfiguration` contains ordered normalized `ContentLayer` records and
a display name. Convenience construction supports the read-only base game and
optional writable active mod; the core API also accepts several named mod layers
with distinct positions and enabled states.

Physical paths and normalized `VirtualPath` values remain separate. Virtual
paths use forward slashes, are relative to a content root, and reject traversal
segments.

## Documents and identity

Every discovered file becomes a `SourceDocument`, including a file that could
not be read or decoded. Document IDs are deterministic SHA-256 identifiers
derived from content-layer ID and virtual path. They remain stable across
reloads while distinguishing the same virtual path in different layers.

A loaded document identifies its source kind and contains `SourceText` plus
either a lossless Clausewitz `SyntaxTree` or `LocalisationSyntaxTree`. It also
retains syntax diagnostics, physical and virtual paths, and layer. A failed
document retains its identity and an `OXIDE3003` diagnostic so one bad file
cannot make the workspace disappear.

## Snapshots and publication

Snapshots are immutable and provide ordered documents plus indexes by document
ID and virtual path. Each successfully published open or reload receives a new
workspace version.

`WorkspaceService` serializes loads. It constructs the next snapshot in
isolation, checks cancellation, and publishes with one atomic reference swap.
A cancelled or failed load leaves the previously published snapshot untouched.
Consumers can observe publication or read `CurrentSnapshot`; they never observe
a partially populated snapshot.

Progress reports distinguish discovery, document loading, publication, and
completion. Discovery and parsing execute on a worker thread.

## Document participation

The loader classifies every discovered document as `Participating`,
`ShadowedByHigherLayerPath`, or `ExcludedByReplacementPath`. An identical
virtual path in a higher layer shadows the lower document. Descriptor
`replace_path` rules exclude lower-layer documents under the replaced directory.
Excluded documents remain loaded, parsed, indexed, and available through the
declaration inventory, but cannot supply an effective semantic contribution.

## Incremental refresh contracts

The workspace defines immutable change contracts independently of any operating-
system watcher. A `WorkspaceChange` records created, changed, deleted, renamed,
or uncertain input with previous/current stable source identities, observation
time, and origin. `WorkspaceChangeBatch` orders those changes deterministically
and can require a reasoned full rescan; uncertainty always escalates rather than
guessing at a partial update.

`SupportedContentProfile` is shared by full discovery and change classification,
so both paths recognize exactly the same state, country-tag, strategic-region,
and localisation files. Classification distinguishes supported files,
unsupported files inside a layer, and paths outside the configured layer root.
Refresh requests, outcomes, and metrics provide the boundary used by the
filesystem change source and later incremental-loader work.

## Filesystem change source

`FileSystemWorkspaceChangeSource` watches every enabled, existing content layer
through a bounded channel. Native callbacks only enqueue raw path events. One
background reader debounces each burst, classifies supported paths through the
shared profile, and publishes an immutable batch. Repeated writes are coalesced;
delete-then-create replacement saves become one change, and temporary files that
are created and deleted within a burst disappear.

Watcher errors, queue overflow, missing roots, uncertain event sequences, and
mod-descriptor changes request a reasoned full rescan. Unsupported paths are
ignored. Renames into or out of supported scope become creations or deletions,
and renames across supported domains become an explicit delete plus create.
Starting, stopping, restarting, and disposal are generation-isolated so an old
watcher cannot deliver events to a replacement workspace. Observer failures are
reported without terminating the watcher worker.

The watcher does not read, parse, build semantics, publish snapshots, or access
the UI.

## Incremental document refresh

`WorkspaceService.RefreshAsync` accepts a request targeting one exact published
snapshot. The loader revalidates every previous and current source identity
against that snapshot's enabled layers and the shared supported-content profile.
It removes deleted identities, loads and parses only created or changed sources,
and reuses unchanged `SourceDocument` instances. Failed reads remain failed,
diagnostic documents rather than disappearing.

The complete candidate document set is reordered and its same-path and
`replace_path` participation is recalculated before semantics are built. Requests
involving uncertain state, descriptors, or configuration use the existing full
discovery path.
Cancellation, stale requests, invalid source identities, and failures cannot
publish over the previous snapshot. Successful publication is one atomic swap
and reports added, changed, removed, reused, and reparsed document counts.

## Semantic invalidation

Incremental refresh uses an explicit domain dependency plan rather than private
cache decisions inside individual builders. The current immutable domains are
countries, states, strategic regions, the province-to-region index,
state-to-region memberships, and localisation. Direct source changes invalidate:

- localisation → localisation only;
- country tags → countries, states, and state-region memberships;
- states → states and state-region memberships; and
- strategic regions → regions, the province index, and state memberships.

Declaration extraction repeats only for directly changed content categories.
Unchanged declaration records and semantic indexes are reused by reference.
Dependent domains rebuild against effective inputs from the candidate snapshot,
and retained domain diagnostics remain present without duplication. Refresh
metrics list the exact rebuilt and reused domains. Full rediscovery rebuilds every
domain; per-entity invalidation remains intentionally deferred until measurements
justify its additional cache complexity.

## Refresh coordination

`WorkspaceRefreshCoordinator` connects a change source to `WorkspaceService`
without placing file work in native watcher callbacks. A bounded command channel
feeds one background consumer, so automatic refreshes never overlap. Bursts that
arrive before processing are coalesced, while changes that arrive during a load
remain pending for the next snapshot. Coordinator overflow escalates to a
reasoned full rescan instead of allocating an unbounded backlog.

Manual reload cancels active incremental work and takes priority over older
queued changes. Replacing or stopping a change source increments a generation,
cancels active work, and prevents old-source commands from reaching the new
workspace. Cancellation and the workspace service's exact-version request check
together prevent stale work from publishing. Observable states distinguish
watching, pending, refreshing, current, failed, unavailable, and stopped; a
failing status observer cannot terminate coordination.

## Snapshot-qualified source navigation

`SourceNavigationTarget` identifies one exact snapshot version, document ID,
content layer, virtual path, source span, semantic identity, and navigation
reason. `SourceNavigationResolver` resolves that target only through the
immutable snapshot indexes; it performs no filesystem access and never
substitutes a same-path declaration from another layer.

Resolution distinguishes an exact location from snapshot-version mismatch,
missing document, source-identity mismatch, failed load, unavailable text,
unsupported document kind, and invalid span. A resolved location includes the
physical and virtual paths, complete layer and participation metadata, document
kind and load state, exact span, and one-based start/end line and column. This
contract is the read-only boundary for the embedded source viewer; rendering,
search, history, and refresh remapping remain later Phase 10 work.

## Diagnostics

Workspace diagnostic codes introduced by this layer are:

- `OXIDE3001`: configured content root does not exist;
- `OXIDE3002`: supported-directory discovery failed; and
- `OXIDE3003`: discovered document could not be read or decoded.

Syntax diagnostics are projected into workspace diagnostics with document ID,
physical path, and source span.

## Current limitations

The core and desktop application implement bounded file watching, incremental
refresh, coordinated cancellation, persisted enablement, and compact status
presentation. Per-entity semantic invalidation remains deferred; affected
domains rebuild as immutable units. The desktop setup supports base game plus one active
mod; ordered dependency-style layers require programmatic configuration. Oxide
does not yet resolve DLC, launcher playsets, dependency order from descriptors,
or a complete province registry. Precedence is implemented only for supported
domains and paths, not as a universal Clausewitz loading rule.
