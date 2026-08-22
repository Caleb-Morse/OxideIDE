# Semantic core

## Scope

Every published workspace snapshot now contains a derived `SemanticSnapshot`.
The first semantic profile recognizes state declarations and country-tag
registrations and turns them into typed, provenance-backed entities.

The source documents and syntax trees remain authoritative. Semantic data is an
immutable, reproducible index and is never serialized back into source.

## Typed identity

An `EntityId` contains an `EntityKind`, namespace, and local key. The first
identity rules are:

- state: `(State, global, numeric ID)`; and
- country: `(Country, tag, normalized uppercase tag)`.

Country declarations retain their original tag spelling even though lookup is
normalized. Numeric state identity is never interchangeable with another
numeric registry.

## Declarations

A `StateDeclaration` records its complete declaration span and sourced
candidates for:

- ID;
- name/localisation key;
- manpower;
- state category;
- resources;
- provinces;
- initial owner; and
- initial cores.

A `CountryTagDeclaration` records the original and normalized tag, definition
path, declaration span, document, and content layer.

Invalid declarations remain in the declaration arrays even when they cannot be
assigned a typed identity. Duplicate property candidates remain inspectable;
Oxide does not silently choose one.

## Provenance and effective values

Every sourced value records its original token text and a `SourceProvenance`
containing document ID, physical path, content layer, and exact text span.

An `EffectiveValue` is created only when this slice has one unambiguous
candidate. It records the selected provenance and selection reason. When an
identity has several declarations, all contributions remain available but no
effective declaration or property is selected because content precedence is
not yet verified.

## Entities and indexes

`StateEntity` and `CountryEntity` implement `ISemanticEntity`. The semantic
snapshot indexes states by numeric ID, countries by normalized tag, and all
entities by typed `EntityId`. Entity status is `Effective`, `Ambiguous`, or
`Invalid`.

State entities expose effective scalar properties, resources and provinces,
plus resolved owner and core references. Country entities expose their
effective definition path when unambiguous.

## Reference outcomes

Country references have explicit outcomes:

- `ResolvedCountry`;
- `MissingCountry`;
- `AmbiguousCountry`; and
- `InvalidCountry`.

Missing and ambiguous references retain their candidate key and provenance.
Ambiguous results retain every country declaration candidate.

## Semantic diagnostics

Codes introduced by this slice are:

- `OXIDE4001`: missing or invalid state block;
- `OXIDE4002`: missing or invalid state ID;
- `OXIDE4003`: duplicate typed entity identity;
- `OXIDE4004`: invalid recognized property value;
- `OXIDE4005`: duplicate recognized property with no selected value;
- `OXIDE4006`: missing country reference;
- `OXIDE4007`: ambiguous country reference; and
- `OXIDE4008`: invalid country tag.

Diagnostics can identify an entity and carry primary and related source
provenance. Syntax, workspace, and semantic diagnostics remain distinct layers.

## Current limitations

This slice does not load country history files or build semantic localisation
declarations, simulate dated history, select among duplicate declarations,
infer mod precedence, or model states beyond the listed properties. Owner and
core extraction is limited to immediate properties of the state's initial
`history` block; dated or scripted changes are not presented as initial
ownership.
