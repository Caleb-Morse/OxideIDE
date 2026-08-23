# Parser, workspace, and semantic robustness

## Safety invariant

Malformed or changing source input must not crash or partially replace an
inspectable workspace. Expected input and file-system failures become bounded
diagnostics. Cancellation remains cancellation, while programming defects are
allowed to surface for correction rather than being hidden as user errors.

## Syntax boundaries

Every lexer iteration must consume at least one UTF-16 code unit. Unicode
whitespace is trivia, including non-breaking and separator characters. A final
recovery guard consumes one bad character and emits `OXIDE1003` if a future
classification path fails to advance.

The generic syntax model recognizes `=`, `==`, `!=`, `<`, `<=`, `>`, `>=`, and
`?=` without assigning domain meaning to them. Syntax remains character-exact
for valid and malformed input. Semantic declaration extractors consume only
ordinary `=` assignments.

Randomized verification is deterministic and bounded by sample count, nesting
depth, and source length. Unrestricted fuzzing does not run as part of local
verification. Any future stress or fuzz job must run separately with an
external timeout and resource containment.

## Workspace boundaries

Discovery failures are reported as `OXIDE3002`. A file that disappears, becomes
unreadable, or cannot be decoded after discovery remains represented as a
failed `SourceDocument` with `OXIDE3003`. Other documents continue loading.

Open and reload construct snapshots away from the published reference. Only a
fully constructed, non-cancelled snapshot is atomically published. Exceptions
or cancellation before publication retain the exact previous snapshot.
Reloads rediscover the supported directories, so added, changed, and removed
files appear together in the next snapshot.

## Semantic boundaries

Semantic extraction tolerates missing blocks, invalid and overflowing IDs,
wrong value shapes, duplicate properties, and invalid or unresolved country
references. It retains valid declarations and structured diagnostics without
manufacturing effective values from ambiguous or malformed candidates.

Strategic-region extraction applies the same boundary to invalid IDs, names,
province blocks, and province entries. Duplicate region identities and competing
province claims remain explicit. Membership is recomputed inside the unpublished
snapshot, so a failed or cancelled reload cannot partially replace the prior
province index or state-membership projection.

## Verification ladder

Robustness changes are checked in increasing scope: compile the affected
project, run focused regression cases, run bounded invariance cases, run
workspace and semantic fixtures, then run the normal repository suite. The
canonical verification command remains the final local gate. GitHub Actions
has an external job timeout and is the appropriate home for future larger
stress suites.
