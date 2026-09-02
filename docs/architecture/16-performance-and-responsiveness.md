# Performance and responsiveness baseline

## Measurement before optimization

The workspace records where full and incremental load time is spent. Full loads
remain the baseline; incremental refresh reuses unchanged documents and
unaffected semantic domains through explicit dependency rules rather than
speculative per-entity caching. A `WorkspaceSnapshot` exposes immutable
`WorkspaceLoadMetrics` for:

- discovered, loaded, and failed document counts;
- workspace and semantic diagnostic counts;
- discovery time;
- document reading, decoding, lexing, and parsing time;
- Clausewitz and localisation document-loading/parsing aggregates;
- semantic construction time;
- localisation indexing time within semantic construction;
- total pre-publication load time; and
- derived document throughput.

The corpus-summary JSON includes these metrics alongside the independently
measured end-to-end duration. It also measures state/country/strategic-region name projection,
derived projection throughput, and managed memory observed at reporting time.
The memory value is not labeled as peak memory; process-level peak resident
memory is captured by the operating system during explicit external runs.

Incremental refresh metrics separately record raw and coalesced changes;
documents added, changed, removed, reused, and reparsed; whether discovery was
escalated to a full rescan; and the exact semantic domains rebuilt or reused.
Timing remains divided across discovery, document loading, semantic rebuilding,
publication, and total refresh. These values are observations rather than
deterministic pass/fail thresholds.

The production corpus report now includes a read-only one-document incremental
probe. It exposes the same document, domain, escalation, and stage observations
in JSON and in one compact human-readable line. This is operational evidence,
not a benchmark: correctness is established separately by full-versus-
incremental semantic equivalence tests. A queue-capacity-one stress scenario
proves that event pressure remains bounded, refresh operations remain serial,
and overflow converges through a full rescan.

Document-kind timings include reading and decoding as well as parsing. They are
named loading/parsing aggregates rather than claiming precision the sequential
loader cannot provide.

## Repeatable scenarios

The normal tests include a deterministic 401-document synthetic scenario. It
opens and reloads the same workspace and verifies that both snapshots expose
complete, internally consistent measurements. Correctness tests deliberately
avoid wall-clock performance thresholds because shared development and CI
machines are variable.

The external category includes a bounded projection scenario that cycles every
language discovered in the extracted corpus and asserts that the published
snapshot object is unchanged. Its generous ceiling detects runaway work rather
than serving as a performance target.

For repeated measurements, run:

```bash
bash scripts/measure-workspace-performance.sh [game-root] [mod-root] [iterations]
```

With no arguments, the command measures the repository synthetic corpus five
times. Each iteration produces a separate ignored JSON file under
`artifacts/performance/`. The external extracted installation can be supplied
explicitly for representative measurements without adding it to normal tests.

## UI-thread boundary

`WorkspaceLoader` schedules discovery, file I/O, decoding, lexing, parsing, and
semantic construction through `Task.Run`. Snapshot construction completes
before the service atomically publishes it. A synchronization-barrier test
holds the worker during discovery and verifies that `OpenAsync` has already
returned an incomplete task and that work is executing on a different thread
from the caller.

The Avalonia view model awaits workspace tasks and only applies the completed
immutable snapshot to observable UI state. Progress now includes the semantic
construction stage, elapsed stage time, and diagnostic count, allowing the UI
to remain informative without performing source work itself.

The embedded source viewer performs no file I/O, decoding, lexing, parsing, or
semantic construction. It projects only immutable snapshot data. Visual source
materialization is capped at 400 lines and 4,000 highlight spans; diagnostic and
find projections are capped at 500 items each; related contributions are capped
at 200; and navigation history is capped at 50 entries. Full snapshot text is
retained by reference for lossless selection and explicit copying rather than
duplicated into visual line objects. Large-source tests assert these ceilings
without imposing unstable wall-clock budgets. The safely scoped external suite
also projects the beginning, midpoint, and end of the largest extracted source
document and verifies the same materialization ceilings.

State-edit eligibility reporting is computed from the immutable snapshot and
performs no live file reads or writes. Edit planning and preview preparation are
in-memory operations; live conflict validation and writing remain asynchronous
application operations rather than UI-thread work. Each before/after edit
preview is capped at 4,000 characters, including a single source line longer
than that ceiling.

The current extracted-corpus baseline loads 2,214 documents, builds 304 region
entities and 13,413 province claims, and derived 1,081 complete state memberships.
Two complete verification runs took about 17–23 seconds, peaked between
approximately 898 MiB and 1.34 GiB resident memory, and recorded zero swap.
These values remain observations, not budgets.

## Baseline policy

Measurements are evidence, not pass/fail budgets. Performance thresholds will
only be introduced after several stable CI and representative external-corpus
runs establish normal variance. Incremental parsing or caching should be
justified by a measured bottleneck and must preserve losslessness, diagnostics,
and atomic snapshot publication.
