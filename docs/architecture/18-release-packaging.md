# Release packaging and clean-environment verification

## Primary distribution model

Oxide development releases use self-contained, platform-specific
packages. A self-contained package carries the matching .NET runtime, making it
larger than a framework-dependent publish but avoiding an installation and
version prerequisite for testers. This is the credible default for an early
desktop application whose users should not need source execution or SDK tools.

Framework-dependent publishing remains useful for developers and managed
environments: downloads are smaller, runtime security servicing is shared, and
one application payload can serve compatible machines. Its runtime prerequisite
and less friendly startup failures make it unsuitable as the primary early
release artifact.

## Supported artifacts

- `win-x64`: self-contained zip containing `app/Oxide.exe`;
- `linux-x64`: self-contained `tar.gz` containing `app/Oxide`;
- `osx-x64`: self-contained zip containing `Oxide.app`; and
- `osx-arm64`: self-contained zip containing `Oxide.app`.

Every archive includes the readme, MIT license, development release notes, and
a SHA-256 checksum. Debug symbols, source/project files, generated reports, and
game installation directories are excluded.

## Verification

The packaging workflow restores the runtime-specific assets, publishes Release
configuration without debug symbols, constructs the platform layout, and then
extracts the finished archive into a new temporary directory. Verification
checks metadata, forbidden content, executable presence and permissions, and—
when the package matches the runner—executes `Oxide --version` with `dotnet`
removed from `PATH`. This headless path confirms that the application host and
bundled runtime start without opening a UI.

GitHub Actions repeats the process on matching hosted operating systems with a
20-minute job timeout. Manual runs retain downloadable workflow artifacts. A
`v*` tag creates a draft GitHub release only after every package job succeeds.

## Trust limitations

Self-contained does not mean trusted or installed. These development packages
are not yet Windows code-signed, Apple Developer ID signed, or notarized.
SmartScreen or Gatekeeper warnings are expected and must be stated in release
notes. Signing, notarization, installers, update channels, and stable versioning
are not yet implemented.
