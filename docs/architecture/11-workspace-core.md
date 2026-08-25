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
Refresh requests, outcomes, and metrics already have stable contracts for later
watcher and incremental-loader work. No filesystem watcher or incremental
publication pipeline is implemented yet.

## Diagnostics

Workspace diagnostic codes introduced by this layer are:

- `OXIDE3001`: configured content root does not exist;
- `OXIDE3002`: supported-directory discovery failed; and
- `OXIDE3003`: discovered document could not be read or decoded.

Syntax diagnostics are projected into workspace diagnostics with document ID,
physical path, and source span.

## Current limitations

The current implementation uses explicit reload rather than file watching. The
desktop setup supports base game plus one active mod; ordered dependency-style
layers require programmatic configuration. Oxide does not yet resolve DLC,
launcher playsets, dependency order from descriptors, or a complete province
registry. Precedence is implemented only for supported domains and paths, not
as a universal Clausewitz loading rule.
