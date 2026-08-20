# User experience model

## Decision

Oxide presents a semantic workspace first and source files second. Files remain
authoritative and always accessible, but the primary interface is organized
around concepts, relationships, and authoring tasks.

The interface must answer four questions wherever the user is:

1. What game concept am I looking at?
2. What is its effective value in this playset?
3. Where did that value come from?
4. What will change in source files if I edit it?

## Workspace shell

The main window has five durable regions:

```text
+-------------------------------------------------------------------+
| Command/search bar                         Playset | Game profile  |
+----------------+----------------------------------+---------------+
| Explorer       | Workspace                        | Inspector     |
|                |                                  |               |
| Concepts       | cards, text, map, graph, table   | properties    |
| Files          | or diff tabs                     | relations     |
| Problems       |                                  | provenance    |
| Layers         |                                  | diagnostics   |
+----------------+----------------------------------+---------------+
| Status: indexing | snapshot | errors | active mod | source mode   |
+-------------------------------------------------------------------+
```

- The explorer changes perspective without changing the underlying workspace.
- The workspace hosts several synchronized views of the selected concept.
- The inspector exposes effective properties, relationships, and provenance.
- The playset and game profile are globally visible because they change meaning.

Panels are rearrangeable, but semantic selection is shared: selecting a state
on the map selects the same state in search, tables, relationships, and source.

## Explorer perspectives

### Concepts

The default perspective groups countries, world, politics, narrative, military,
economy, script, and assets. It supports filters such as “defined by my mod,”
“affected by my mod,” “has errors,” and “overrides vanilla.”

### Files

The physical and virtual file views remain available for experienced authors.
The virtual view overlays identical paths from every active content layer and
shows which files are effective, hidden, or merged.

### Relationships

This perspective begins from an entity and explores inbound and outbound edges,
with filters for relationship kind, confidence, layer, and runtime conditionality.

### Problems

Diagnostics are grouped by concept by default, with file, severity, source
layer, and diagnostic-code alternatives. A problem can reveal its source span,
related definitions, and safe fixes.

### Content layers

The layer perspective explains base game, DLC, dependencies, and active-mod
precedence. It can compare declarations and preview what a different playset
would make effective.

## Concept pages

Every concept page uses a consistent structure:

- header: display name, typed identity, source status, active layer, and health;
- overview: the most important properties for the concept;
- specialized views: map, graph, timeline, table, image, audio, or script;
- relationships: both outgoing dependencies and incoming usage;
- sources: contributing declarations, effective-selection explanation, and diff;
- problems: diagnostics scoped to the concept; and
- history: session edits and external changes, not simulated game history.

Specialized views are projections over the same selected entity. They do not
create private copies of its data.

### Country workspace

The country workspace exposes overview, map and territory, government and
ideas, characters, focus trees, events and decisions, military, presentation,
references, and sources. It brings together many entities but clearly labels
their individual identities and ownership.

### State workspace

The state workspace exposes map geometry, constituent provinces, ownership and
cores, resources and buildings, victory points, strategic region and supply,
script references, localisation, and sources. Selecting a province refines the
inspector without navigating away from the state.

### Focus-tree workspace

The focus-tree workspace uses a graph canvas with structural validation. A
selected focus exposes requirements, mutual exclusions, conditions, rewards,
AI weights, localisation, icon, and source. External references remain visible
as graph boundary nodes rather than disappearing.

### Event-chain workspace

The event workspace provides a navigable event graph and a focused event form.
It distinguishes proven callers, dynamic possible callers, and missing targets.
Triggers and effects may be shown in structured form while their source text is
one action away.

## Progressive disclosure

The default surface uses player and mod-author language. Engine identifiers,
raw keys, load-policy details, and source spans are available without dominating
the initial view.

Three information depths are supported:

- overview: human name, major facts, health, and common actions;
- inspect: complete properties, relationships, provenance, and alternatives;
- source: exact declarations, syntax, raw files, and resolution trace.

The depths are views of the same entity, not beginner and expert modes with
different truth.

## Search and navigation

Global search accepts display names, typed IDs, localisation keys, paths, and
script identifiers. Results show kind, effective status, owning layer, and a
short relationship-aware summary. Ambiguous text such as `123` is grouped by
kind rather than guessed.

Navigation supports:

- go to concept;
- go to effective declaration;
- go to a specific contributing declaration;
- find all references;
- show dependency or impact graph;
- reveal on map or graph; and
- return through semantic navigation history.

## Editing experience

Forms and visual editors express semantic intentions. Before saving, Oxide shows
a source-impact summary: files created or changed, layers affected, declarations
overridden, and diagnostics introduced or repaired.

Routine unambiguous edits may apply immediately with normal undo. Creation,
rename, deletion, cross-file changes, and edits requiring a new override use an
explicit preview. The preview can switch between semantic summary and exact
text diff.

When a value comes from vanilla or a dependency, editing offers “override in
active mod” and explains the smallest safe override known for that entity kind.
If that behavior is not verified, Oxide offers source navigation rather than an
unsafe generated edit.

## Uncertainty and conflicts

Oxide never communicates uncertain data solely through colour. Each uncertain
value has a label and explanation:

- effective: selected by a verified or documented rule;
- inferred: likely result with stated evidence;
- ambiguous: several candidates cannot be distinguished;
- missing: a static reference has no candidate;
- dynamic: runtime script prevents static resolution;
- shadowed: present but excluded by precedence; or
- invalid: syntax or schema prevents interpretation.

The user can inspect every candidate and resolution trace. Concept pages remain
usable when part of the model is invalid.

## Saved UI metadata

Oxide may save workspace preferences such as open concept tabs, map viewport,
graph node positions, explorer filters, and panel layout. This metadata lives in
an Oxide project area and never changes game behavior.

Graph positions already defined by game data remain semantic properties. A
user-only arrangement of an analysis graph is UI metadata. The distinction is
explicit so opening a view never dirties the mod.

## First interface slice

The first interface slice is a read-only world explorer:

1. open a game installation and active mod;
2. search or browse countries and states;
3. select a state from a table or simple map;
4. inspect owner, cores, provinces, resources, and strategic region;
5. navigate to related countries and source declarations;
6. compare effective and shadowed contributions; and
7. review missing or ambiguous references.

It should use production semantic APIs, not temporary UI-specific parsing. A
successful slice demonstrates the interaction contract that later focus-tree,
event, asset, and editing work can reuse.
