# Lossless syntax core

## Scope

`Oxide.Syntax` provides a game-agnostic structural parser for Clausewitz text.
It recognizes assignments, blocks, scalar values, and bare values without
assigning HOI4 meaning to any key or value.

## Fidelity contract

The token stream retains identifiers, quoted strings, numbers, dates, braces,
equals signs, comments, whitespace, newlines, malformed tokens, and end of
file. `SyntaxTree.ToFullString()` reconstructs the decoded source exactly.
For supported source encodings, `SyntaxTree.GetOriginalBytes()` returns the
original bytes, including a UTF-8 byte-order mark when present.

The first implementation supports strict UTF-8 with or without a byte-order
mark. Inputs that cannot be decoded as strict UTF-8 must be treated as
unsupported by the workspace layer rather than silently converted.

## Public model

- `SourceText` owns decoded text, encoding metadata, newline metadata, byte
  fidelity, line starts, and offset-to-line mapping.
- `ClausewitzLexer` produces an immutable complete token stream and lexical
  diagnostics.
- `ClausewitzParser` produces a `SyntaxTree` containing the source, tokens,
  generic root, and combined diagnostics.
- `PropertySyntax`, `BlockValueSyntax`, `ScalarValueSyntax`, and
  `BareValueSyntax` are the generic structural forms consumed by later schema
  extractors.
- `MissingValueSyntax`, missing tokens, and `UnexpectedTokenSyntax` preserve a
  usable tree when the input is malformed.

Node spans cover their meaningful source text. Trivia remains in the complete
token stream rather than being discarded or normalized.

## Diagnostics

Syntax diagnostic codes are stable identifiers:

- `OXIDE1001`: unterminated quoted string;
- `OXIDE1002`: unexpected control character;
- `OXIDE2001`: property missing a value;
- `OXIDE2002`: expected value;
- `OXIDE2003`: block missing a closing brace;
- `OXIDE2004`: unexpected token; and
- `OXIDE2005`: expected token.

Recovery diagnostics do not prevent consumers from inspecting successfully
parsed siblings or the original token stream.

## Explicit non-goals

This layer does not know which keys are valid, interpret scopes, resolve
references, apply content precedence, normalize formatting, or rewrite files.
Those responsibilities belong to schemas, semantics, and edit planning.
