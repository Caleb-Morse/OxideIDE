# Semantic core

## Scope

Every published workspace snapshot now contains a derived `SemanticSnapshot`.
The semantic profile recognizes state declarations, country-tag registrations,
and language-qualified localisation declarations and turns them into immutable,
provenance-backed indexes.

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

A `LocalisationDeclaration` is identified by `LocalisationLanguage` plus the
case-sensitive `LocalisationKey`. It records the decoded value, optional version,
complete declaration provenance, and exact value provenance. Multiple declarations
with the same identity are retained as contributions to one `LocalisationEntry`.

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

The localisation index is keyed by `(language, key)`. `LocalisationResolver`
returns explicit resolved, missing, ambiguous, or invalid outcomes. It first tries
the requested language and may fall back to English only when the exact key is
missing. It never selects from duplicate candidates. Resolved outcomes carry the
selected declaration and exact value provenance. `ResolveName` applies this same
policy to state name keys and country tags, returning a deterministic identifier
fallback when no human-readable value can be selected.

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
- `OXIDE4007`: ambiguous country reference;
- `OXIDE4008`: invalid country tag; and
- `OXIDE4009`: duplicate language-qualified localisation identity.

Diagnostics can identify an entity and carry primary and related source
provenance. Syntax, workspace, and semantic diagnostics remain distinct layers.

## Current limitations

This slice does not load country history files, infer ideology-specific country
name conventions, simulate dated history, select among duplicate declarations,
infer mod precedence, or model states beyond the listed properties. Owner and core
extraction is limited to immediate properties of the state's initial `history`
block; dated or scripted changes are not presented as initial ownership.
