# Domain model

## Purpose

Oxide's domain model is the stable vocabulary shared by indexing, navigation,
validation, editing, and the user interface. It describes both game-level
entities and higher-level authoring concepts without pretending that they have
the same identity or lifetime.

The model has four levels:

```text
source documents -> declarations -> semantic entities -> concept projections
```

- A source document is a physical file.
- A declaration is one meaningful contribution extracted from source syntax.
- A semantic entity is the resolved game-level identity in a workspace snapshot.
- A concept projection is a user-oriented composition of entities and relationships.

Only source documents are persisted by Oxide as mod content. The remaining
levels are reproducible indexes. Project settings, layout preferences, and
other IDE metadata may be persisted separately, but they are not game content.

## Common semantic shape

Every semantic entity exposes a common envelope:

```text
SemanticEntity
  Id                 typed canonical identity
  Kind               schema-defined entity kind
  Display             resolved name, icon, and summary
  Status              effective, shadowed, conflicting, incomplete, or invalid
  Contributions[]     declarations that define or affect it
  Properties{}        effective values with per-value provenance
  References[]        outgoing typed relationships
  ReferencedBy[]      incoming typed relationships
  Diagnostics[]       errors, warnings, uncertainty, and suggestions
  Capabilities[]      operations valid for this entity in this workspace
```

`Display` and `Capabilities` are derived. They do not belong in the parser or
become part of entity identity. A state can remain a state when its localised
name is missing; a read-only vanilla country does not acquire an edit capability
until Oxide can create a safe override in the active mod.

## Entity families

Entity kinds are grouped by how authors think about the game. Families are for
navigation and schema organization; they do not create additional identities.

### World and geography

- `Province`: numeric province identity, map colour, type, terrain, coastal
  status, continent, and adjacency relationships.
- `State`: numeric state identity, provinces, owner, cores, claims, resources,
  buildings, manpower, category, and victory points.
- `StrategicRegion`: numeric region identity, member provinces, weather, and
  naval or air context.
- `SupplyNode` and `Railway`: supply-network nodes and ordered province paths.
- `MapAdjacency`: explicit exceptional adjacency, such as a strait or canal.

Some map data has no natural named declaration in script. Oxide still assigns a
typed identity derived from the game registry, while retaining the exact bitmap,
CSV row, or script span that supplied it.

### Countries and politics

- `Country`: tag-based country identity and definition.
- `CountryHistory`: dated and undated initial conditions associated with a
  country. This remains separately inspectable even when projected into a country.
- `Character`: character identity, roles, portraits, traits, and country links.
- `Ideology`, `SubIdeology`, `CountryLeaderTrait`, and `UnitLeaderTrait`.
- `Idea`, `IdeaCategory`, `Decision`, `DecisionCategory`, and `BalanceOfPower`.
- `AutonomyState` and other political registries defined by a game profile.

### Narrative and progression

- `Event`: namespaced event identity, trigger, options, effects, pictures, and
  follow-up event references.
- `FocusTree` and `NationalFocus`: tree membership, placement constraints,
  prerequisites, mutual exclusions, availability, rewards, and AI weights.
- `Technology`: technology identity, tree placement, unlocks, modifiers, and
  dependencies.
- `Achievement`, `Bookmark`, and other player-facing progression structures.

### Military and economy

- `UnitType`, `SubUnit`, `DivisionTemplate`, and `OrderOfBattle`.
- `Equipment`, `EquipmentVariant`, and `EquipmentArchetype`.
- `Building`, `Resource`, `Modifier`, and `ModifierDefinition`.
- `MilitaryIndustrialOrganization`, policy, trait, and related organization nodes.

The exact boundaries between entities such as equipment and archetypes are
profile-driven. Oxide should expose inheritance or derivation as relationships,
not flatten it into an unexplained final object.

### Script and runtime symbols

- `ScriptedEffect`, `ScriptedTrigger`, `ScriptedModifier`, `ScriptedValue`, and
  `OnAction`.
- `Variable`, `Flag`, `EventTarget`, and `ScopeExpression` as symbolic entities
  or references when their runtime identity cannot be proven statically.

These entities require confidence-aware analysis. An unresolved dynamic target
is not equivalent to a missing static target.

### Presentation and assets

- `LocalisationEntry`: language-qualified localisation key and value.
- `Sprite`, `Portrait`, `Texture`, `Sound`, `MusicTrack`, and `RadioStation`.
- `InterfaceDefinition`, `Font`, `Particle`, and other presentation registries.

Asset entities distinguish logical names used by script from physical files.
Several logical sprites may reference one texture, and one entity may select
different localisation entries by language.

## Relationships

Relationships are typed, directional, and independently addressable. They are
not merely object pointers. A relationship records its source span, resolution
state, schema meaning, and optional ordering or temporal context.

Initial relationship kinds include:

- composition: state `contains` province, focus tree `contains` focus;
- ownership: country `owns` state at a date;
- claims and cores: country `has core on` state;
- dependency: focus `requires` focus, technology `unlocks` equipment;
- control flow: event option `fires` event, on-action `invokes` effect;
- presentation: entity `uses localisation`, `uses sprite`, or `uses portrait`;
- membership: character `belongs to` country, state `belongs to` region;
- script access: effect `reads`, `writes`, or `tests` a symbol; and
- provenance: effective property `supplied by` declaration.

Relationships may carry conditions. “Germany owns state 64” at game start and
“an event may transfer state 64 to Germany” are different facts and must not be
collapsed into one unconditional edge.

## Values, expressions, and time

An entity property is not always a primitive value. The value model includes:

- scalar and structured literals;
- ordered and unordered collections as declared by schema;
- localisation and asset references;
- script expressions and blocks;
- conditional values;
- dated history entries; and
- computed effective values with provenance.

Oxide initially models historical dates as ordered source contributions rather
than simulating arbitrary runtime state. The interface must label start-date
facts, dated changes, possible runtime effects, and inferred facts distinctly.

## Concept projections

A concept projection composes several semantic entities for a user task. It has
a stable projection key within a snapshot, but it is not a new game object and
is never serialized as one.

Initial projections are:

- `CountryConcept`: definition, history, politics, characters, states, focus
  trees, ideas, events, decisions, technology relationships, flags, portraits,
  localisation, and inbound references.
- `StateConcept`: map geometry, provinces, ownership history, cores, resources,
  buildings, victory points, supply, strategic region, and referencing script.
- `FocusTreeConcept`: tree structure, focus graph, shared branches, rewards,
  conditions, AI weights, localisation, icons, and external dependencies.
- `EventChainConcept`: event graph, triggers, options, effects, pictures,
  localisation, callers, and possible next events.
- `MilitaryConcept`: unit, equipment, technology, production, organisation, and
  template relationships for a selected country or scope.
- `AssetConcept`: logical asset registrations, physical files, previews, and all
  consumers.

Projections are assembled by registered providers. A provider declares its
required entity kinds and relationships so incremental invalidation can rebuild
only affected projections.

## Authoring operations

The semantic API exposes intentions rather than file mutations:

- create, duplicate, rename, or remove an entity;
- set or remove an effective property;
- add, remove, or reorder a relationship;
- move a map member or graph node;
- extract reusable scripted logic;
- create a safe override in the active mod; and
- repair a missing or ambiguous reference.

Each intention is planned into a previewable `WorkspaceEdit`. Availability is
represented by an entity capability with a reason when disabled. This lets the
same interface work for writable mod content, read-only vanilla content, and
uncertain semantic states without offering unsafe actions.

## Extensibility

Entity kinds, property schemas, identity rules, relationship extractors,
resolution policies, display providers, and editor capabilities are registered
by a versioned `GameProfile`. Core types remain generic enough to support new
HoI4 releases and mod-defined conventions while typed schema packages provide
the rich behavior expected for known content.

Extensions may add knowledge, but may not bypass provenance, replace the
lossless source model, or claim certainty stronger than their evidence.

## First implementation subset

The first vertical slice implements only:

- `Country`, `LocalisationEntry`, `Province`, `State`, and `StrategicRegion`;
- ownership, core, province membership, region membership, and localisation
  relationships;
- `CountryConcept` and `StateConcept` read-only projections;
- effective and complete views; and
- navigation, provenance, and missing-reference diagnostics.

This subset is deliberately small but proves that a concept can combine data
from several file formats without losing the underlying source boundaries.
