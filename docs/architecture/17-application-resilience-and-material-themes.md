# Application resilience, persistence, and material themes

## Recoverable application state

The application treats workspace loading, saved preferences, and presentation
state as separate boundaries. A failed or cancelled workspace load returns to
the welcome screen or the last published workspace. A settings failure never
removes a successfully loaded snapshot. Errors remain visible in a dismissible
notification rather than replacing the current content.

Oxide restores the last successfully opened game and active-mod paths but does
not automatically reopen them. This avoids surprising disk activity at launch
and lets a user correct paths after moving or removing an installation.

## Settings storage

Settings use a versioned JSON document under the operating system's application
data directory. Saves write a temporary sibling file and replace the settings
file only after serialization and flushing complete. Missing settings use
defaults. Corrupt, unreadable, or unsupported settings fall back to defaults
with a visible warning.

The current schema stores:

- last successful game root;
- last successful active-mod root;
- selected material theme;
- preferred localisation language; and
- whether English localisation fallback is enabled.

Language settings are preferences rather than workspace facts. Opening a workspace
that lacks the preferred language selects a deterministic effective language without
overwriting the stored preference. Older schema-version-one files that omit these
fields receive English and enabled-fallback defaults.

Workspace source, installation data, semantic snapshots, and generated reports
are never copied into settings.

## Material theme language

Oxide themes describe the surfaces of real metals rather than generic dark and
light modes:

- **Iron Rust Dark** is the default and reflects Hearts of Iron: charcoal iron
  surfaces, warm off-white text, and orange-red iron-oxide accents.
- **Copper Verdigris Light** uses warm pale copper/mineral surfaces, dark
  oxidized text, and blue-green patina accents.

Both palettes use Avalonia theme variants so native controls switch their full
dark/light behavior. Application colors are dynamic resources; changing theme
updates the existing window without reconstructing the workspace or view
model. Contrast-sensitive foreground, border, muted, and error colors are
defined separately for each material palette.

## UX behavior

The header exposes the alternate material theme from both the welcome and
workspace screens. Loading reports discovery, document, semantic, and
publication stages and remains cancellable. Empty selections retain guidance,
and load or persistence errors can be dismissed without losing the active
workspace.
