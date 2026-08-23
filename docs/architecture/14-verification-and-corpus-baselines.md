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
- a base/mod virtual-path collision;
- English, Spanish, Russian, and Simplified Chinese localisation;
- exact, English-fallback, missing, no-key, and ambiguous name outcomes;
- duplicate localisation identities within one file and across layers;
- versions, escaped quotes, and malformed localisation; and
- source-backed values whose spans are checked against their documents.

Invalid byte sequences remain covered by focused workspace tests because a
reviewable text fixture cannot itself contain invalid UTF-8.

Its expected counts are asserted in tests, making semantic or diagnostic
changes explicit during review.

## External corpus verification

Run installation-dependent verification explicitly:

```sh
./scripts/verify-external-corpus.sh /path/to/hoi4 [optional-mod-root]
```

This restores and builds Release, runs only the four external-corpus tests,
and writes `artifacts/external-corpus-summary.json` through the production
reporter.

The current extracted-installation baseline is:

- 1,910 discovered and loaded documents;
- zero failed documents;
- zero syntax diagnostics;
- 1,081 state declarations and entities;
- 439 country declarations and entities;
- four semantic `OXIDE4005` diagnostics; and
- 2,612 resolved country references with zero unresolved references;
- 827 localisation documents and four discovered languages;
- 535,329 language-qualified declarations and identities, all with valid
  provenance in the extract;
- 1,081 exact English state names; and
- 325 exact English country names with 114 direct-tag names missing.

Load duration is reported for observation but is not a deterministic baseline
or CI failure threshold.

## Corpus summary contract

`CorpusSummaryBuilder` derives a report from one published immutable workspace
snapshot, a measured total duration, and an explicit language/fallback policy.
It reports:

- files discovered;
- documents loaded and failed;
- syntax diagnostic total and counts by code;
- workspace diagnostic counts by code;
- state declaration and entity counts;
- country declaration and entity counts;
- semantic diagnostic total and counts by code;
- resolved, missing, ambiguous, invalid, and total unresolved country
- references;
- localisation file, load, language, declaration, identity, duplicate,
  diagnostic, and provenance counts;
- exact, English-fallback, missing, ambiguous, invalid, and no-key state and
  country name outcomes;
- requested and effective report language plus fallback policy;
- name projection duration, throughput, and managed memory observed when the
  report is created; and
- total loading milliseconds.

`Oxide.CorpusSummary` is the command-line host for this production report. It
accepts `--game-root`, optional `--mod-root`, optional `--name`, optional
`--output`, optional `--language`, and `--no-english-fallback`. JSON is always
emitted to standard output; a compact health summary is emitted to standard
error so redirected automation retains clean JSON.

The canonical synthetic report requests Spanish with English fallback enabled,
deliberately exercising exact, fallback, missing, ambiguous, and no-key paths.
Tests assert exact structural counts and normalize only volatile timing and
memory observations when checking repeatability.

## Repository safeguards

Root-level game installation directories are ignored, including `history`,
`common`, `map`, `localisation`, `events`, `gfx`, `sound`, `music`, and
`interface`. The canonical verification also fails when any generated
`artifacts` path is tracked or when an unexpected file larger than 10 MiB exists
outside Git metadata, build output, or generated artifacts.

Synthetic fixtures remain allowed because they live below `Oxide.Tests` and
are intentionally small, reviewable, original inputs.
