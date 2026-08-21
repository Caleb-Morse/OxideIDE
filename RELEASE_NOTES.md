# Oxide development release

This is an early development build of Oxide's read-only Hearts of Iron IV
workspace explorer.

## Included

- Open a Hearts of Iron IV installation and optional active mod.
- Inspect states, country references, provenance, and diagnostics.
- Reload an immutable workspace after source files change.
- Iron Rust Dark and Copper Verdigris Light material themes.
- Persist the last successful workspace paths and selected theme.

## Important limitations

- Editing is not enabled.
- Only state history and country-tag files are currently modeled.
- Packages are not yet code-signed or notarized. Windows SmartScreen and macOS
  Gatekeeper may display warnings for downloaded builds.
- Keep backups of mods and game data even though this release opens them
  read-only.

Report reproducible problems with the platform, package name, Oxide version,
and the visible diagnostic or error message.
