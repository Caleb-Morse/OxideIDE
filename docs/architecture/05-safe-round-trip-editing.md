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

## Transactions and conflicts

Multi-file operations are a single `WorkspaceEdit` containing versioned edits.
All preconditions are checked before any file is written. Writes use temporary
files and atomic replacement where the platform permits it. If external changes
invalidate a precondition, the entire operation stops and is recalculated.

Every workspace edit supports preview and undo. Undo records inverse text edits,
not merely a semantic command, so it restores exact formatting.

## Formatting

Formatting is an explicit command with a separately documented style. Normal
semantic edits format only newly inserted syntax and nearby separators. A
formatter must never be an implicit prerequisite for using a visual editor.
