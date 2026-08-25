# Application slice

## Purpose

The first Avalonia application slice exposes the complete read-only pipeline:

```text
selected roots -> workspace documents -> syntax trees -> semantic snapshot -> state and country browsers
```

It uses production workspace and semantic APIs. The application does not parse
files or implement game rules in the presentation layer.

## Screens

### Open workspace

The welcome screen accepts a required Hearts of Iron IV installation and an
optional active-mod directory. Native folder pickers are available, while paths
can also be entered directly. The screen states the currently supported content
and read-only behavior.

### Loading

Opening and reloading show discovery, document loading, snapshot publication,
and completion progress. Loading can be cancelled. Cancellation preserves an
already-published workspace and never exposes a partial snapshot.

### Language-aware workspace

The Settings menu exposes every language discovered in the immutable semantic snapshot
with a readable native label and a canonical internal identifier. Preferred and
effective language are separate: the persisted preference is used when available,
English is the secondary choice, and the first discovered language is the final
deterministic choice. An unavailable preference is retained and becomes effective
again when a later snapshot provides it. Switching language rebuilds only the
presentation projections and performs no file I/O, parsing, or workspace reload.

English fallback is an independently persisted, default-enabled preference.
Resolved names distinguish exact-language matches from English fallback. Disabling
fallback immediately reprojects missing values from the existing snapshot. Missing
and ambiguous values display stable state IDs or country tags rather than silently
selecting a candidate; an ambiguous requested-language value never falls through to
English.

### State workspace

The workspace provides:

- a virtualized state list;
- filtering by translated name, ID, name key, owner, category, or source path;
- synchronized state selection;
- overview values for owner, category, manpower, cores, resources, and
  provinces;
- a language-aware strategic-region result with single, split, partial, missing,
  ambiguous, and no-province states kept distinct;
- per-province state-side and region-side membership provenance;
- semantic status and diagnostic count; and
- effective declaration path, content layer, and line/column provenance; and
- selected localisation source, language, resolution reason, and line/column.

The first state is selected after a successful open. Reload retains the prior
state selection when the same identity remains available.

### Country workspace

The country browser provides translated names alongside stable tags, definition
paths, declaration status, localisation provenance, and searchable source context.
Owned and core state memberships are derived from the same published snapshot.
Owned and core state buttons return to the corresponding state without discarding
the workspace. State and country detail views expose the effective declaration,
all shadowed or competing contributions, field comparisons, and localisation
resolution paths with exact provenance.

### Problems

Workspace, syntax, and semantic diagnostics are projected into one Problems
list while retaining their distinct codes. A state-associated problem selects
the related state and clears a filter if necessary to reveal it.

### Status

The durable header shows the workspace name and counts for files, states, and
countries. The footer shows snapshot version and error/warning counts. Users
can reload or return to workspace selection without restarting Oxide.

## Presentation boundary

`MainWindowViewModel` owns loading, cancellation, search, projections,
selection, and recoverable errors. `MainView` contains only Avalonia window
lifetime, native folder selection, and event forwarding. View models consume
`IWorkspaceService` and are covered without creating UI controls.

`StateListItemViewModel` and `CountryListItemViewModel` are display projections
over semantic entities; they do not parse or reinterpret game source. Both use the
shared `LocalisationResolver`. State region display is projected from the
snapshot's immutable membership index; it performs no file access or parsing.
Provenance is resolved through the published snapshot's document index.

## Current limitations

This slice is read-only. It has no map, embedded source editor, automatic file
watching, saved recent-workspace list, playset selection, DLC resolution, or
dependency-mod ordering. Country naming currently recognizes the direct tag key;
ideology-qualified naming remains future work. Provenance paths and exact source
locations are visible, but navigation into an embedded source viewer awaits that
viewer. Source actions already publish an exact `SourceNavigationRequest`, and
the footer confirms its file and span; only embedded rendering remains deferred.
