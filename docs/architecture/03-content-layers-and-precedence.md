# Content layers and precedence

## Decision

Oxide builds an explicit `LoadPlan` and resolves each virtual path and entity
kind through registered policies. There is no universal “last file wins” rule.

## Content layers

A workspace may contain:

1. base game content;
2. enabled DLC content;
3. dependency mods in playset order; and
4. the active mod.

Each `ContentLayer` has a stable ID, kind, root path, descriptor metadata,
enabled state, declared dependencies, and its position in the resolved load
plan. A layer may be present for inspection without being active.

## Virtual paths

Files are addressed by normalized paths relative to their content root. Oxide
retains physical paths separately. This allows files from several roots to
participate in the same game directory without conflating them on disk.

`replace_path` is modeled as a visibility rule on a virtual directory. It can
hide lower-layer content before declaration-level resolution occurs. Hidden
files remain indexed for explanation and comparison but cannot contribute to
the effective model.

## Resolution policies

A policy is selected using virtual directory, file type, and entity kind. The
initial policy vocabulary is:

- `MergeFiles`: all visible files contribute;
- `ReplaceVirtualFile`: the highest-precedence file at a virtual path contributes;
- `FirstDeclarationWins`;
- `LastDeclarationWins`;
- `MergeDeclarations`: declarations combine according to a schema;
- `Coexist`: duplicate keys are permitted in a defined scope; and
- `Conflict`: no reliable effective entity can be selected.

Policies may also define deterministic ordering within a layer, but ordering
must be verified against the game rather than assumed from filesystem order.

## Effective and complete views

The semantic index exposes both:

- an effective view approximating what the game loads; and
- a complete view containing hidden, shadowed, invalid, and conflicting input.

The UI must never show an effective value without offering its provenance and
the declarations it displaced.

## Unknown behavior

Unverified directories use `UnknownResolution`. Oxide may display candidates
and diagnostics but must not pretend one is effective. Policies live in
versioned game profiles so different HoI4 versions can behave differently.
