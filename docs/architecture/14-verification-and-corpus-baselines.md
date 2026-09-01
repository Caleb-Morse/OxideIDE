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
- an ambiguous country reference;
- a base/mod virtual-path collision;
- English, Spanish, Russian, and Simplified Chinese localisation;
- exact, English-fallback, missing, no-key, and ambiguous name outcomes;
- duplicate localisation identities within one file and across layers;
- versions, escaped quotes, and malformed localisation; and
- source-backed values whose spans are checked against their documents;
- valid, malformed, repeated, and duplicate strategic-region declarations;
- resolved, split, partial, missing, ambiguous, and no-province state-region
  memberships; and
- state-side and region-side membership provenance.

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
reporter. The reporter performs one read-only incremental probe by rereading an
existing participating document; it never writes to the supplied installation
or mod root.

The current extracted-installation baseline is:

- 2,214 discovered and loaded documents;
- zero failed documents;
- zero syntax diagnostics;
- 1,081 state declarations and entities;
- 439 country declarations and entities;
- 304 strategic-region declarations and effective entities;
- 13,413 unique province claims, with no repeated or ambiguous claims;
- all 1,081 states resolving completely to one strategic region;
- four semantic `OXIDE4005` diagnostics;
- 2,612 resolved country references with zero unresolved references;
- 827 localisation documents and four discovered languages;
- 535,329 language-qualified declarations and identities, all with valid
  provenance in the extract;
- 1,081 exact English state names;
- 325 exact English country names with 114 direct-tag names missing; and
- 303 exact English strategic-region names with one missing key.

Two full extracted-corpus verification runs completed in about 17–23 seconds, with
approximately 898 MiB–1.34 GiB maximum resident memory and zero swap. These
resource figures are observations, not deterministic failure thresholds.

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
- strategic-region file, declaration, entity, effective/ambiguous, province
  candidate, repeated candidate, indexed province, ambiguity, and provenance counts;
- single, split, partial, missing, ambiguous, and no-province state membership counts;
- semantic diagnostic total and counts by code;
- resolved, missing, ambiguous, invalid, and total unresolved country
  references;
- localisation file, load, language, declaration, identity, duplicate,
  diagnostic, and provenance counts;
- exact, English-fallback, missing, ambiguous, invalid, and no-key state,
  country, and strategic-region name outcomes;
- contribution-resolution totals by supported domain, content layer, and
  disposition, including multi-contribution identities, cross-layer overrides,
  same-layer duplicates, invalid winners, and missing identities;
- requested and effective report language plus fallback policy;
- name projection duration, throughput, and managed memory observed when the
  report is created; and
- total loading milliseconds; and
- one incremental probe outcome, trigger, raw/coalesced event counts,
  reparsed/reused documents, full-rescan decision, exact rebuilt/reused semantic
  domains, and stage timings.

`Oxide.CorpusSummary` is the command-line host for this production report. It
accepts `--game-root`, optional `--mod-root`, optional `--name`, optional
`--output`, optional `--language`, and `--no-english-fallback`. JSON is always
emitted to standard output; a compact health summary is emitted to standard
error so redirected automation retains clean JSON.

The canonical synthetic report requests Spanish with English fallback enabled,
deliberately exercising exact, fallback, missing, ambiguous, and no-key paths,
plus layered overrides, excluded documents, same-layer duplicates, and a
whole-value localisation alias.
Tests assert exact structural counts and normalize only volatile timing and
memory observations when checking repeatability. Cross-domain tests compare an
incremental semantic fingerprint with a clean full reload. Coordinator stress
tests constrain the command queue to one entry, force overflow, and verify
serialized recovery through one reasoned full rescan.

Source-navigation verification covers exact snapshot resolution, every supported
semantic identity domain, effective and shadowed relationships, bounded history,
forward-branch removal, compatible manual and automatic refresh remapping, stale
layer removal, and watcher failure without publication. A 20,600-line stress
fixture verifies the default viewer ceilings of 400 materialized lines, 4,000
highlight spans, 500 diagnostics, and 500 search results. A 205-contribution
identity verifies the 200-item application relationship ceiling. These are
structural bounds rather than machine-dependent timing thresholds.

## Repository safeguards

Root-level game installation directories are ignored, including `history`,
`common`, `map`, `localisation`, `events`, `gfx`, `sound`, `music`, and
`interface`. The canonical verification also fails when any generated
`artifacts` path is tracked or when an unexpected file larger than 10 MiB exists
outside Git metadata, build output, or generated artifacts.

Synthetic fixtures remain allowed because they live below `Oxide.Tests` and
are intentionally small, reviewable, original inputs.
