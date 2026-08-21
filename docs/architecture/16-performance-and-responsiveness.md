# Performance and responsiveness baseline

## Measurement before optimization

The workspace records where load time is spent without introducing
incremental parsing, caches, file watchers, or speculative optimization. A
`WorkspaceSnapshot` exposes immutable `WorkspaceLoadMetrics` for:

- discovered, loaded, and failed document counts;
- workspace and semantic diagnostic counts;
- discovery time;
- document reading, decoding, lexing, and parsing time;
- semantic construction time;
- total pre-publication load time; and
- derived document throughput.

The corpus-summary JSON includes these metrics alongside the independently
measured end-to-end duration. This distinguishes internal stage cost from the
observable command duration.

## Repeatable scenarios

The normal tests include a deterministic 401-document synthetic scenario. It
opens and reloads the same workspace and verifies that both snapshots expose
complete, internally consistent measurements. Correctness tests deliberately
avoid wall-clock performance thresholds because shared development and CI
machines are variable.

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

## Baseline policy

Measurements are evidence, not pass/fail budgets. Performance thresholds will
only be introduced after several stable CI and representative external-corpus
runs establish normal variance. Incremental parsing or caching should be
justified by a measured bottleneck and must preserve losslessness, diagnostics,
and atomic snapshot publication.
