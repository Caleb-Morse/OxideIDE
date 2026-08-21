# Verification and corpus baselines

## Canonical verification

The supported local and CI verification entry point is:

```sh
./scripts/verify.sh
```

It performs, in order:

1. repository-content safeguards;
2. solution restore;
3. Debug build;
4. Release build;
5. all normal unit, integration, architecture, application-flow, and synthetic
   corpus tests;
6. formatting verification; and
7. production corpus-summary generation over the repository-owned fixtures.

Both configurations treat warnings as errors through `Directory.Build.props`.
The generated summary is written to
`artifacts/synthetic-corpus-summary.json`; `artifacts` is ignored by Git.

GitHub Actions invokes the same script and uploads the synthetic summary as a
build artifact. CI does not implement a second, divergent sequence of commands.

## Test categories

Normal automation excludes only tests marked `Category=ExternalCorpus`. Those
tests require a local HOI4 installation or extract and are not suitable for a
clean checkout or hosted CI.

The compact corpus under `Oxide.Tests/Fixtures/Corpus` is original synthetic
content. It is copied into test output and runs in normal automation under
`Category=SyntheticCorpus`. It deliberately contains:

- valid state and country declarations;
- malformed syntax;
- an invalid state ID;
- invalid resource and province values;
- a missing country reference;
- ambiguous state and country identities;
- an ambiguous country reference; and
- a base/mod virtual-path collision.

Its expected counts are asserted in tests, making semantic or diagnostic
changes explicit during review.

## External corpus verification

Run installation-dependent verification explicitly:

```sh
./scripts/verify-external-corpus.sh /path/to/hoi4 [optional-mod-root]
```

This restores and builds Release, runs only the three external-corpus tests,
and writes `artifacts/external-corpus-summary.json` through the production
reporter.

The current extracted-installation baseline is:

- 1,083 discovered and loaded documents;
- zero failed documents;
- zero syntax diagnostics;
- 1,081 state declarations and entities;
- 439 country declarations and entities;
- four semantic `OXIDE4005` diagnostics; and
- 2,612 resolved country references with zero unresolved references.

Load duration is reported for observation but is not a deterministic baseline
or CI failure threshold in Phase 5.1.

## Corpus summary contract

`CorpusSummaryBuilder` derives a report from one published immutable workspace
snapshot and a measured total duration. It reports:

- files discovered;
- documents loaded and failed;
- syntax diagnostic total and counts by code;
- workspace diagnostic counts by code;
- state declaration and entity counts;
- country declaration and entity counts;
- semantic diagnostic total and counts by code;
- resolved, missing, ambiguous, invalid, and total unresolved country
  references; and
- total loading milliseconds.

`Oxide.CorpusSummary` is the command-line host for this production report. It
accepts `--game-root`, optional `--mod-root`, optional `--name`, and optional
`--output`; JSON is always emitted to standard output.

## Repository safeguards

Root-level game installation directories are ignored, including `history`,
`common`, `map`, `localisation`, `events`, `gfx`, `sound`, `music`, and
`interface`. The canonical verification also fails when any such root-level
path is tracked or when an unexpected file larger than 10 MiB exists outside
Git metadata, build output, or generated artifacts.

Synthetic fixtures remain allowed because they live below `Oxide.Tests` and
are intentionally small, reviewable, original inputs.
