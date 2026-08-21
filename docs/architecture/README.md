# Oxide architecture

Oxide turns a Hearts of Iron IV installation and one or more mods into an
inspectable semantic workspace without hiding or replacing the source files.
The source text remains authoritative; entities, relationships, diagnostics,
and visual editors are derived projections.

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

## Documents

1. [Source and semantic models](01-source-and-semantic-models.md)
2. [Identity and namespaces](02-identity-and-namespaces.md)
3. [Content layers and precedence](03-content-layers-and-precedence.md)
4. [References and resolution](04-references-and-resolution.md)
5. [Safe round-trip editing](05-safe-round-trip-editing.md)
6. [Incremental reparsing](06-incremental-reparsing.md)
7. [Verification and open questions](07-verification-and-open-questions.md)
8. [Domain model](08-domain-model.md)
9. [User experience model](09-user-experience-model.md)
10. [Lossless syntax core](10-syntax-core.md)
11. [Workspace core](11-workspace-core.md)
12. [First semantic core](12-semantic-core.md)
13. [First application slice](13-first-application-slice.md)
14. [Verification and corpus baselines](14-verification-and-corpus-baselines.md)
15. [Parser, workspace, and semantic robustness](15-parser-workspace-semantic-robustness.md)
16. [Performance and responsiveness baseline](16-performance-and-responsiveness.md)
17. [Application resilience and material themes](17-application-resilience-and-material-themes.md)
18. [Release packaging and clean-environment verification](18-release-packaging.md)

## Non-goals for the first vertical slice

- perfectly simulate the runtime game state;
- evaluate every trigger or effect;
- rewrite entire files into an Oxide-preferred format;
- infer undocumented precedence behavior without fixtures; or
- require all files to be valid before providing navigation and editing.

## Initial vertical slice

The recommended first slice is a read-only state explorer:

1. open a vanilla installation and one mod;
2. discover `history/states/*.txt`;
3. parse state IDs, names, owners, cores, resources, and province lists;
4. resolve country tags and localisation;
5. display source provenance and diagnostics; and
6. navigate from a state to every known reference.

This exercises all core boundaries while keeping editing and map rendering out
of the first milestone.
