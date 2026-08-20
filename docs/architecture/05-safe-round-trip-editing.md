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
