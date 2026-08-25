# Semantic core

## Scope

Every published workspace snapshot now contains a derived `SemanticSnapshot`.
The semantic profile recognizes state declarations, strategic-region declarations,
country-tag registrations, and language-qualified localisation declarations and
turns them into immutable, provenance-backed indexes.

The source documents and syntax trees remain authoritative. Semantic data is an
immutable, reproducible index and is never serialized back into source.

## Typed identity

An `EntityId` contains an `EntityKind`, namespace, and local key. The first
identity rules are:

- state: `(State, global, numeric ID)`; and
- country: `(Country, tag, normalized uppercase tag)`; and
- strategic region: `(StrategicRegion, global, numeric ID)`.

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

A `StrategicRegionDeclaration` records the complete declaration span, ID and
name candidates, and every ordered province-membership candidate. Invalid IDs,
malformed province entries, repeated province candidates, and duplicate blocks
remain diagnostic and source-backed.

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

An `EffectiveValue` records the selected value, exact provenance, selection
reason, and any ignored candidates. Entity and localisation declarations use a
shared `ContributionResolution`: every candidate is classified as effective,
shadowed, ambiguous, invalid, or excluded. The supported domains use
`LayeredOverride` policy—higher participating layers override lower layers,
while competing valid declarations in the winning layer remain ambiguous.
No losing declaration is discarded.

## Entities and indexes

`StateEntity`, `CountryEntity`, and `StrategicRegionEntity` implement
`ISemanticEntity`. The semantic snapshot indexes states and regions by numeric
ID, countries by normalized tag, and all entities by typed `EntityId`. Entity
status is `Effective`, `Ambiguous`, or `Invalid`.

State entities expose effective scalar properties, resources and provinces,
plus resolved owner and core references. Country entities expose their
effective definition path when unambiguous. Strategic-region entities expose an
effective name key and ordered province candidates when their identity is unique.

`ProvinceStrategicRegionIndex` retains every region-side claim for each province.
It returns resolved, missing, or ambiguous outcomes; repeated claims by the same
effective region remain resolved with all provenance, while competing region IDs
or an ambiguous region identity remain ambiguous. Derived state memberships are
classified as single-region, split, partial, missing, ambiguous, or no-provinces.
Every province reference retains the state-side value provenance and every
region-side candidate provenance.

The localisation index is keyed by `(language, key)`. `LocalisationResolver`
returns explicit resolved, missing, ambiguous, or invalid outcomes. It first
resolves the requested language by layer and may fall back to English only when
that exact key is missing; fallback then performs its own layer resolution.
Same-layer duplicates remain ambiguous. Whole-value aliases are followed through
a bounded, provenance-preserving reference chain. `ResolveName` applies this same
policy to state name keys, country tags, and strategic-region name keys, returning
a deterministic identifier fallback when no human-readable value can be selected.

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
- `OXIDE4010`–`OXIDE4015`: invalid strategic-region declaration shapes,
  identities, names, province blocks, and repeated province candidates;
- `OXIDE4016`: ambiguous province-to-region claims; and
- `OXIDE4017`–`OXIDE4021`: ambiguous, split, partial, missing, or no-province
  state membership.

Diagnostics can identify an entity and carry primary and related source
provenance. Syntax, workspace, and semantic diagnostics remain distinct layers.

## Current limitations

This slice does not load country history files, infer ideology-specific country
name conventions, simulate dated history, infer launcher dependency ordering,
model a complete province registry, type strategic-region weather, or model
states beyond the listed properties. Same-layer duplicates intentionally remain
ambiguous. Owner and core
extraction is limited to immediate properties of the state's initial `history`
block; dated or scripted changes are not presented as initial ownership.
