# Oxide roadmap

This document describes Oxide's intended delivery order and the boundary of
each upcoming phase. It is a planning guide, not an architectural contract or a
record of every change. Implemented guarantees live in
[the architecture reference](architecture/README.md); pull requests and
releases provide the authoritative delivery history.

The ordering may change when verification against real Hearts of Iron IV data
reveals a missing prerequisite. A planned phase should not be read as a promise
that its behavior is already available.

## Current position

Phases 1 through 11 established the hardened vertical slice:

- lossless Clausewitz syntax, workspace loading, and source-backed semantics;
- state, country, localisation, and strategic-region inspection;
- layered effective and shadowed contributions;
- bounded automatic refresh and source navigation;
- release packaging and clean-environment verification; and
- conflict-safe minimal editing of existing writable state `manpower` and
  `state_category` values, with preview and exact-byte one-level undo.

The next planned phase is Phase 12. Oxide still opens a base installation plus
one optional active mod directly; it does not yet provide an Oxide project
format or resolve launcher playsets, DLC, and ordered dependency mods.

## Phase 12 — Project creation, import, and playset-aware configuration

**Outcome:** A user can create or import a mod project and understand the exact
ordered source configuration Oxide will inspect and edit.

Planned work:

1. discover supported game installations, launcher registrations, local mods,
   and workshop mods without broad or unbounded filesystem scanning;
2. introduce source-backed installation, mod-registration, descriptor,
   dependency, supported-version, and writable-root concepts;
3. define a versioned, portable Oxide project format;
4. preview and transactionally create a minimal mod and descriptor pair;
5. import existing registered or unregistered mods without rewriting them;
6. read supported launcher playsets safely and provide a manual fallback;
7. resolve base game, applicable DLC, dependency mods, the active writable mod,
   and `replace_path` rules into one inspectable load plan;
8. replace the basic workspace picker with create, import, recent-project,
   playset, and load-order surfaces;
9. refresh or migrate project and playset configuration without replacing the
   last valid snapshot on failure; and
10. add synthetic multi-mod fixtures, safely scoped launcher/mod examples,
    corpus reporting, and current-guarantee documentation.

Phase 12 is complete when project creation and import are safe and previewable,
all participating and excluded content layers have an explanation, writable
and read-only roots are unmistakable, configuration persists predictably,
load-plan changes preserve atomic publication, and canonical plus safely scoped
external verification pass.

Non-goals include modifying launcher databases, editing the game installation,
claiming undocumented launcher order as verified game order, and launching the
game.

Relevant architecture:

- [Content layers and precedence](architecture/03-content-layers-and-precedence.md)
- [Workspace core](architecture/11-workspace-core.md)
- [Domain model](architecture/08-domain-model.md)
- [User experience model](architecture/09-user-experience-model.md)
- [Verification and open questions](architecture/07-verification-and-open-questions.md)

## Phase 13 — Focus-tree and event-chain tooling

**Outcome:** A user can inspect and navigate effective focus trees and supported
event relationships against the selected project and playset.

Planned work includes source-backed focus-tree, focus, event-namespace, event,
option, and relationship concepts; layered contribution resolution; localized
focus and event names; prerequisite and mutual-exclusion edges; direct,
delayed, dynamic, missing, and ambiguous event calls; focus-to-event links;
bounded read-only graph views; source navigation; incremental refresh; and
external-corpus reporting.

Read-only semantic and relationship tooling comes before structural graph
editing. Oxide must not pretend that arbitrary triggers, effects, scopes, or
dynamic event targets can always be evaluated statically.

Relevant architecture:

- [Domain model](architecture/08-domain-model.md)
- [User experience model](architecture/09-user-experience-model.md)
- [References and resolution](architecture/04-references-and-resolution.md)

## Phase 14 — Validation runs and game-launch integration

**Outcome:** A user can run reproducible static validation and explicitly
authorized game launches against an exact installation, project, playset, load
plan, and workspace snapshot.

Planned work includes versioned validation-run records, launch-command preview,
process lifecycle and cancellation, bounded log discovery and tailing, mapping
supported log entries back to source, persisted run history, and a clear
distinction between static-analysis success and observed runtime loading.

Oxide will not silently change launcher state, launch without an explicit user
action, or claim that a successful launch proves arbitrary script behavior is
correct.

Relevant architecture:

- [Domain model](architecture/08-domain-model.md)
- [User experience model](architecture/09-user-experience-model.md)
- [Verification and corpus baselines](architecture/14-verification-and-corpus-baselines.md)

## Beyond Phase 14

Later work may include safe insertion and override creation, richer validation
and refactoring, focus-tree and event authoring, project packaging, map and
asset tooling, compatibility analysis across game versions and playsets, and
additional typed domains. These are directions rather than scheduled phases;
they should be promoted into numbered plans only when their prerequisites and
acceptance criteria are understood.

## Maintaining this roadmap

- Update phase scope here when planning changes materially.
- Mark outcomes complete only after their acceptance criteria pass.
- Keep durable rules and guarantees in `docs/architecture`.
- Keep detailed implementation history in pull requests and releases.
- Link to existing documents instead of duplicating their technical models.
