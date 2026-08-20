# Identity and namespaces

## Decision

Entity identity is a typed, normalized key rather than an unqualified string.

```text
EntityId = (EntityKind, Namespace, LocalKey)
```

Examples include `(State, global, 123)`, `(Country, tag, FRA)`, and
`(Event, my_mod, political.4)`. The displayed name and localisation are
properties, not identity.

## Identity rules

Identity rules are registered per entity kind because HoI4 has no universal ID
scheme. A rule specifies:

- where the key is declared;
- whether comparison is case-sensitive;
- normalization permitted for lookup;
- namespace rules;
- uniqueness scope; and
- whether filename or virtual path participates in identity.

Oxide keeps the original spelling even when lookup uses a normalized key.
Numeric keys are not interchangeable across kinds: state `123`, province `123`,
and a numeric variable value are different concepts.

## Namespaces

Namespaces are explicit where the game exposes them, such as event namespaces.
For globally unique registries, Oxide uses a logical namespace representing that
registry. File paths are not invented as namespaces unless the game uses them to
determine identity.

Dynamic names such as variables, flags, event targets, scripted values, and
localisation expressions may not always have statically knowable identities.
They are represented as typed symbolic references with an expression payload,
not forced into a concrete entity ID.

## Aliases and composites

An entity can have typed aliases without changing its canonical identity. A
country, for example, may be addressed through a tag, a dynamic tag alias, or a
runtime scope. Alias resolution records uncertainty and context.

A user-facing concept such as “France” is an aggregate view over several entity
kinds and files. It is not a new game-level identity. A `ConceptView` gathers
country definition, history, characters, focuses, flags, localisation, and
references while retaining their individual identities and provenance.

## Duplicate declarations

Duplicate identity is not automatically an error or an override. The entity
kind's resolution policy determines whether declarations merge, replace,
coexist, or conflict. All duplicates remain visible in the semantic index.
