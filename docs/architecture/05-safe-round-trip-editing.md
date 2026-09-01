# Safe round-trip editing

## Decision

Oxide uses a lossless concrete syntax tree and minimal text edits. It does not
serialize semantic entities back into whole files.

## Preservation contract

Parsing followed by emitting an unchanged tree must reproduce the original
bytes except where decoding policy makes byte preservation impossible. In that
case the document is read-only until the user explicitly chooses a conversion.

The source layer preserves:

- comments and whitespace;
- key and value spelling;
- quoting and escapes;
- ordering and duplicate keys;
- newline convention and final newline state;
- byte-order mark and supported encoding; and
- malformed or unknown syntax.

## Edit operations

Feature and visual editors produce semantic intentions such as “change the
owner of state 123.” An edit planner then:

1. identifies the effective property and its provenance;
2. chooses an existing writable declaration or proposes a new override;
3. creates minimal source text changes;
4. applies them to an expected document version;
5. reparses the changed region; and
6. verifies the intended semantic result before saving.

If several source locations are valid, Oxide presents the alternatives. It does
not silently edit vanilla or DLC files. By default, changes target the active
mod and create the smallest safe override supported by that entity policy.

## Implemented contract boundary

The workspace core now defines the first editing contracts without enabling
file mutation. A `WorkspaceEdit` is snapshot-qualified and contains one or more
non-overlapping `TextChange` values grouped by exact source document. Each
`DocumentEditTarget` retains the document and layer identities, virtual and
physical paths, snapshot version, and a SHA-256 fingerprint of the original
snapshot bytes.

`EditCapabilityEvaluator` is a pure, snapshot-only gate. It permits a document
only when its provenance is exact, its declaration is unambiguous, its source
layer is writable and participating, its encoding is supported, its load
succeeded, and it has no source errors. Refusals remain explicit: read-only
layer, stale snapshot, ambiguous declaration, malformed source, unsupported
encoding or operation, external conflict, missing provenance, and failed
document are distinct outcomes.

Preview, application, and exact-byte undo result types are defined so later
sub-phases share one safety vocabulary. No planner, writer, application editing
surface, or automatic override creation is implemented at this boundary.

The in-memory preparation stage is also implemented. It validates every target
against the immutable snapshot and its original-byte fingerprint, applies
non-overlapping changes from the end of the source toward the beginning, encodes
the result with the original UTF-8 BOM policy, and reparses the updated source
according to its document kind. Because offsets refer only to the original
snapshot, insertions and replacements may freely change the number of characters
or lines without shifting another planned change. Two insertions at one exact
position must be combined by the planner rather than relying on incidental sort
order.

Preparation rejects stale snapshots, mismatched source identities or
fingerprints, unavailable or non-editable documents, out-of-range spans,
unencodable replacements, and updated text with parser errors. It produces
preview text, updated bytes and fingerprints, syntax trees, and diagnostics in
memory only. Semantic intent verification, disk-conflict checks, and writing
remain later boundaries.

## First semantic planner

The first planner supports replacing an existing `manpower` or
`state_category` scalar on a state whose single effective declaration is already
in the writable active-mod layer. It resolves the effective state through the
shared contribution model, requires exactly one sourced candidate for the
selected property, and replaces only that candidate's value span. It validates
manpower as a non-negative integer and state category as one unquoted Clausewitz
identifier.

After preparing the candidate text, the planner extracts state declarations
again and proves that exactly one declaration for the intended state remains and
that it contains exactly the requested semantic value. Missing properties,
duplicate properties, ambiguous declarations, read-only base sources, invalid
values, and semantic no-ops are explicit refusals. The planner does not yet
insert missing properties, create mod overrides, write files, or publish a new
workspace snapshot.

## Pre-write validation

`WorkspaceEditPreflightValidator` is the final read-only boundary before a
writer. It first repeats in-memory preparation against the supplied
immutable snapshot. Only a valid candidate advances to live validation. It then
reads every target file and compares its SHA-256 fingerprint with the bytes from
which the edit was planned.

Results distinguish ready, rejected candidate, external conflict, filesystem
failure, and cancellation. A changed or deleted file is a conflict; an
inaccessible file is a failure. Multi-document validation examines every target
but is ready only when all targets still match, so a caller cannot interpret a
partially valid set as permission to write. The live fingerprints are retained
in the successful result for the writer's immediate conflict checks. This stage
does not write temporary files, replace originals, or mutate the workspace, and
its success cannot eliminate the need for the writer to guard the race between
validation and replacement.

## Conflict-safe writing

`WorkspaceEditWriter` accepts only an edit that can pass the complete preflight
sequence. It writes every candidate to a uniquely named sibling staging file,
flushes the bytes to stable storage, preserves Unix file permissions where
applicable, and repeats live fingerprint validation after staging. Immediately
before each replacement it checks the target fingerprint once more. A conflict
before the first replacement leaves every original untouched.

Each target is replaced with the platform's atomic file-replacement primitive,
which creates a sibling backup of the exact original. Once replacement begins,
caller cancellation is deferred until the set is consistent. If a later
replacement fails, Oxide restores backups in reverse order. Successful results
contain an undo record with the original bytes and applied fingerprint. Staging
and backup artifacts are removed after success or successful rollback.

If automatic rollback itself fails, the result is failed rather than applied,
the surviving backups are retained, and their exact paths are returned as
recovery artifacts. Cleanup failures are also visible warnings. Oxide does not
claim impossible cross-file atomicity: common filesystems provide atomic rename
or replacement for one file, not one indivisible transaction covering several
paths. The coordinated backup-and-rollback protocol prevents ordinary partial
failure, but a process or machine crash during a multi-file commit can leave
recovery artifacts for a later recovery workflow. Directory-entry durability
also remains subject to the host filesystem and operating system.

## Transactions and conflicts

Multi-file operations are a single `WorkspaceEdit` containing versioned edits.
All preconditions are checked before any original is replaced. Writes use
sibling staging files, per-file atomic replacement, exact backups, and
coordinated rollback. If external changes invalidate a precondition before
replacement, the entire operation stops and is recalculated.

Every workspace edit supports preview and undo. Undo records inverse text edits,
not merely a semantic command, so it restores exact formatting.

## Formatting

Formatting is an explicit command with a separately documented style. Normal
semantic edits format only newly inserted syntax and nearby separators. A
formatter must never be an implicit prerequisite for using a visual editor.
