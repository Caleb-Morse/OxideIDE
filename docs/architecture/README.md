# Oxide architecture

Oxide turns a Hearts of Iron IV installation and one or more mods into an
inspectable semantic workspace without hiding or replacing the source files.
The source text remains authoritative; entities, relationships, diagnostics,
and visual editors are derived projections.

This directory is the architectural reference for contributors. It is not a
development diary. A reader should be able to use it to understand the system,
its design decisions, its implemented guarantees, and its known limitations
without reconstructing the order in which features were built. Milestone
history and delivery notes belong in pull requests and releases.

## How to read and maintain this directory

New contributors should read this page, then the implemented architecture
pages relevant to their work. The product-direction documents are useful when
designing beyond today's supported slice; they are not promises that every
described concept is already implemented.

Architecture documents should:

- describe one durable concern with a clear scope;
- distinguish current guarantees from intended direction;
- link to an existing page instead of repeating its model or rationale;
- record rationale and constraints, but not a chronological account of work;
- use pull requests, release notes, and issue tracking for phase status and
  task history; and
- be updated in place when an existing concern changes. Add a new page only
  when it owns a genuinely separate architectural concern.

## Design goals

- Present concepts such as countries, states, events, and focuses as coherent
  entities even when their data is spread across many files.
- reproduce the game's effective view of vanilla, DLC, dependencies, and the
  active mod while retaining every contributing definition;
- make every semantic fact traceable to an exact source span;
- support text and visual editing without destroying comments, formatting, or
  constructs Oxide does not yet understand;
- update the workspace quickly after file changes; and
- treat uncertainty as visible data rather than silently guessing.

## Core data flow

```text
Workspace roots and playset
        |
        v
Virtual file system and load plan
        |
        v
Lossless syntax trees ----> source edits
        |
        v
Declarations and references
        |
        v
Resolution policies and semantic index
        |
        +----> diagnostics
        +----> entity inspector
        +----> navigation and search
        +----> map, focus-tree, and other visual editors
```

## Architectural boundaries

- `Oxide.Syntax`: tokens, trivia, lossless trees, parsing, and text changes.
- `Oxide.Workspaces`: roots, descriptors, playsets, virtual paths, file
  discovery, watching, snapshots, and load plans.
- `Oxide.Semantics`: schemas, declarations, identities, references, resolution,
  merged entities, provenance, and diagnostics.
- `Oxide.Features`: reusable operations such as find references, rename,
  navigation, validation, and entity-oriented edits.
- `Oxide.App`: Avalonia views and interaction. It must not contain parsing or
  game-resolution rules.

These names express boundaries, not a requirement to create five assemblies
immediately. Dependencies point inward: the UI may consume semantic services,
but the semantic layer must not depend on Avalonia.

## Document map

### Implemented architecture

Start here to understand the code that exists today:

- [Lossless syntax core](10-syntax-core.md)
- [Workspace core](11-workspace-core.md)
- [Semantic core](12-semantic-core.md)
- [Application slice](13-first-application-slice.md)
- [Parser, workspace, and semantic
  robustness](15-parser-workspace-semantic-robustness.md)
- [Application resilience and material
  themes](17-application-resilience-and-material-themes.md)

### Verification and operations

These pages define how architectural claims are measured and delivered:

- [Verification and corpus baselines](14-verification-and-corpus-baselines.md)
- [Performance and responsiveness](16-performance-and-responsiveness.md)
- [Release packaging and clean-environment
  verification](18-release-packaging.md)

### Cross-cutting reference

- [Logical ERD reference](19-erd-reference.md) maps the implemented model and
  longer-term generic semantic model into relational notation. It is a
  communication aid, not evidence of a database persistence layer.

### Product direction and foundational decisions

These pages define the broader model Oxide is growing toward. Some portions
are intentionally ahead of the implementation:

- [Source and semantic models](01-source-and-semantic-models.md)
- [Identity and namespaces](02-identity-and-namespaces.md)
- [Content layers and precedence](03-content-layers-and-precedence.md)
- [References and resolution](04-references-and-resolution.md)
- [Safe round-trip editing](05-safe-round-trip-editing.md)
- [Incremental reparsing](06-incremental-reparsing.md)
- [Verification strategy and open questions](07-verification-and-open-questions.md)
- [Domain model](08-domain-model.md)
- [User experience model](09-user-experience-model.md)

## Non-goals for the current vertical slice

- perfectly simulate the runtime game state;
- evaluate every trigger or effect;
- rewrite entire files into an Oxide-preferred format;
- infer undocumented precedence behavior without fixtures; or
- require all files to be valid before providing navigation and editing.

## Current vertical slice

The implemented application is a read-only state explorer that can:

1. open a vanilla installation and one mod;
2. discover state, strategic-region, country-tag, and multilingual localisation source files;
3. parse state IDs, name keys, owners, cores, resources, and province lists;
4. index language-qualified localisation declarations and resolve exact-language or
   English-fallback values without hiding duplicates;
5. resolve state and country display names through one provenance-backed service;
6. resolve state owner and core references against country-tag registrations;
7. resolve every state province against all strategic-region claims without
   hiding split, partial, missing, or ambiguous outcomes;
8. switch among discovered languages without reloading the workspace;
9. browse countries and navigate their owned or core state memberships; and
10. display declaration, localisation, and two-sided region-membership provenance
    alongside diagnostics;
11. open exact snapshot source with bounded line, highlight, diagnostic, search,
    relationship, and history projections; and
12. conservatively preserve compatible source navigation across refreshes while
    reporting stale declarations instead of substituting another layer.

The core also exposes a snapshot-qualified editing capability and contract
boundary: it can explain why an exact source is or is not eligible for a future
minimal edit and can bind proposed changes to the original bytes. It does not
yet plan or write those changes, so the application remains read-only. Map
rendering also remains product direction rather than a current capability.
