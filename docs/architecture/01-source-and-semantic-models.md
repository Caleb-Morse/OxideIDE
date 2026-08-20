# Source and semantic models

## Decision

Oxide maintains separate source, declaration, and semantic models. None may be
used as a lossy replacement for another.

## Source model

A `SourceDocument` represents one physical file and contains:

- stable document ID;
- content-layer and absolute/virtual paths;
- encoding, newline convention, and content hash;
- immutable text snapshot and version;
- lossless syntax tree; and
- syntax diagnostics.

The syntax tree preserves every token and trivia item, including whitespace,
comments, quoting, malformed input, and unknown constructs. Nodes carry source
spans but do not claim game meaning.

## Declaration model

A declaration is a candidate semantic contribution extracted from syntax using
a schema. Examples include a state block, country-tag entry, event, focus,
sprite, localisation entry, or reference-bearing property.

Every declaration records:

- entity kind and candidate identity;
- defining syntax node and precise source spans;
- content layer and load position;
- extracted properties without discarding their syntax origins; and
- the schema and schema version used to interpret it.

Multiple declarations may describe, extend, replace, or conflict with the same
identity. They must remain independently inspectable.

## Semantic model

A `SemanticEntity` is the effective workspace interpretation of one identity.
It contains:

- canonical identity;
- all contributing declarations;
- effective values and provenance for each value;
- outgoing and incoming references;
- shadowed, conflicting, and unresolved contributions; and
- semantic diagnostics.

Entities are immutable within a workspace snapshot. A refresh creates new
entities and atomically publishes a new snapshot.

## Provenance

Every effective property must answer:

1. which file and span supplied this value;
2. which declarations were ignored or shadowed;
3. which resolution rule selected it; and
4. whether the result is known, inferred, ambiguous, or invalid.

Visual editors operate on semantic entities but produce source-level edits
against the declaration that owns the selected property.
