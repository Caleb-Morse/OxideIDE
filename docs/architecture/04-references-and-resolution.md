# References and resolution

## Decision

References are first-class records in a bidirectional semantic index. Resolution
returns a structured result, never a nullable entity.

## Reference record

A reference records:

- reference kind and expected target kinds;
- source document, syntax node, and token span;
- original text;
- source entity when one exists;
- scope and schema context;
- candidate identity or dynamic expression; and
- resolution result for the current workspace snapshot.

## Resolution results

```text
Resolved(target)
Missing(candidate key)
Ambiguous(candidates, reason)
Dynamic(expression, constraints)
Invalid(reason)
NotApplicable
```

Missing targets remain indexed so creating the target later repairs all related
diagnostics automatically. Ambiguous references retain every candidate and the
rule that failed to distinguish them.

## Context

The same token can mean different things depending on schema position and
scope. A number may be a state ID in one property and a scalar in another. A
country tag, variable, event target, or scope expression may occupy the same
syntactic shape. Resolution therefore starts from a schema-classified reference,
not from token text alone.

Scope-flow analysis is incremental and may be partial. Where Oxide cannot prove
the current scope, it reports possible target kinds rather than inventing one.

## Indexes

Each snapshot provides:

- declarations by typed identity;
- outgoing references by source entity and document;
- incoming references by target identity;
- unresolved references by candidate key; and
- diagnostics by document and entity.

These indexes support go-to-definition, find-references, concept views,
dependency visualization, impact analysis, and safe rename previews.

## Diagnostics

Diagnostics carry severity, stable code, message, source span, related spans,
workspace version, and optional fixes. Missing and ambiguous references are
different diagnostic classes. A suppressed or shadowed declaration can still
produce informational diagnostics without contaminating the effective model.
