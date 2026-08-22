# Application slice

## Purpose

The first Avalonia application slice exposes the complete read-only pipeline:

```text
selected roots -> workspace documents -> syntax trees -> semantic snapshot -> state browser
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

### State workspace

The workspace provides:

- a virtualized state list;
- filtering by ID, name key, owner, category, or source path;
- synchronized state selection;
- overview values for owner, category, manpower, cores, resources, and
  provinces;
- semantic status and diagnostic count; and
- effective declaration path, content layer, and line/column provenance.

The first state is selected after a successful open. Reload retains the prior
state selection when the same identity remains available.

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

`StateListItemViewModel` is a display projection over `StateEntity`; it does not
copy or reinterpret game source. Provenance is resolved through the published
snapshot's document index.

## Current limitations

This slice is read-only. It displays localisation keys rather than translated
names, has no map, source editor, automatic file watching, saved recent
workspaces, playset selection, DLC resolution, or dependency-mod ordering.
Problems navigate to related states but not yet to an embedded source editor.
