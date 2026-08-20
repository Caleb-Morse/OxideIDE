# Incremental reparsing

## Decision

Oxide publishes immutable workspace snapshots and updates them through a
dependency-driven pipeline. Correct full rebuilds come first; incremental reuse
is an optimization with identical observable results.

## Change pipeline

```text
filesystem/editor change
  -> classify affected content layer and virtual path
  -> update text snapshot
  -> lex and parse changed document
  -> extract declarations and references
  -> invalidate affected identities and resolution policies
  -> re-resolve impacted references and aggregate concepts
  -> publish snapshot and diagnostic delta
```

Rapid file-watcher events are debounced and coalesced. Open editor buffers take
precedence over disk content until saved or closed. Self-generated save events
are recognized by document version/hash rather than ignored by timing alone.

## Invalidation levels

- Text-only: trivia changes with unchanged syntax structure.
- Document: declarations or references in one document changed.
- Identity: all candidates and references for specific typed identities change.
- Directory policy: filenames, virtual paths, or `replace_path` affect a policy.
- Load plan: playset, dependency, DLC, or descriptor changes require broad
  visibility and resolution recomputation.
- Schema/profile: game-version knowledge changed; rebuild semantics globally.

The dependency graph maps documents to declarations, declarations to identities,
identities to references, and entities to aggregate concept views. Invalidation
walks this graph instead of rescanning every file.

## Concurrency

Parsing may run in parallel because documents are independent inputs. Snapshot
publication is atomic. Cancellation abandons obsolete work when a newer edit
arrives. Consumers request a snapshot version and never observe partially
updated indexes.

## Performance targets

Targets are provisional until measured on a complete vanilla installation:

- visible syntax feedback within 100 ms of a local edit;
- updated references and entity diagnostics within 500 ms for an ordinary file;
- no UI-thread parsing or file I/O; and
- bounded memory through shared immutable text and syntax structures.

Benchmarks must include large modifier/reference files, state histories,
localisation, and burst changes from external tools.
