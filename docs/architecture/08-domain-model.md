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

The four levels describe game content, not the entire authoring workspace.
Mod descriptors, playsets, validation runs, version baselines, and change plans
are authoring objects with different lifetimes. Oxide models them explicitly
without pretending that they are declarations loaded by the game.

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

### Diplomacy and world rules

- `Faction`, `WarGoal`, `OpinionModifier`, and `ScriptedDiplomaticAction`.
- `GameRule`, `DifficultySetting`, and `Bookmark` configuration.
- `OccupationLaw`, resistance and compliance definitions, and peace-conference
  behavior.

These systems often combine registries, scripted conditions, and hard-coded
engine behavior. A semantic model may describe the verified declarations and
relationships without claiming to reproduce the complete runtime system.

### Narrative and progression

- `Event`: namespaced event identity, trigger, options, effects, pictures, and
  follow-up event references.
- `FocusTree` and `NationalFocus`: tree membership, placement constraints,
  prerequisites, mutual exclusions, availability, rewards, and AI weights.
- `Technology`: technology identity, tree placement, unlocks, modifiers, and
  dependencies.
- `Doctrine`, `ContinuousFocus`, `SpecialProject`, `Specialization`,
  `ScientistTrait`, and related research structures.
- `Achievement` and other player-facing progression structures.

### Military and economy

- `UnitType`, `SubUnit`, `DivisionTemplate`, and `OrderOfBattle`.
- `Equipment`, `EquipmentVariant`, and `EquipmentArchetype`.
- `Building`, `Resource`, `Modifier`, and `ModifierDefinition`.
- `MilitaryIndustrialOrganization`, policy, trait, and related organization nodes.
- `Ability`, `Medal`, `Ace`, `RaidType`, and related military registries.

The exact boundaries between entities such as equipment and archetypes are
profile-driven. Oxide should expose inheritance or derivation as relationships,
not flatten it into an unexplained final object.

### Intelligence and operations

- `IntelligenceAgency`, `IntelligenceAgencyUpgrade`, and operative-related
  registries exposed by the game profile.
- `Operation`, `OperationPhase`, `OperationToken`, and `TimedActivity`.

Operations demonstrate why a declaration is not necessarily a self-contained
entity: phases, equipment requirements, scopes, outcomes, assets, events, and
localisation combine into the feature experienced by a player.

### AI configuration

- `AIStrategy`, `AIStrategyPlan`, `AIFocus`, `AITemplate`, and equipment, naval,
  area, or theatre configuration.
- AI weights and strategy blocks attached to other entities remain properties
  or contributions of those entities when they have no independent identity.

Static analysis can explain declared weights, conditions, and references. It
must not present them as a prediction of emergent AI behavior.

### Script and runtime symbols

- `ScriptedEffect`, `ScriptedTrigger`, `ScriptedModifier`, `ScriptedValue`, and
  `OnAction`.
- `ScriptedLocalisation`, `ScriptConstant`, `ScriptCollection`, and
  `MeanTimeToHappen` definitions where supported by the game profile.
- `Variable`, `Flag`, `EventTarget`, and `ScopeExpression` as symbolic entities
  or references when their runtime identity cannot be proven statically.

These entities require confidence-aware analysis. An unresolved dynamic target
is not equivalent to a missing static target.

### Presentation and assets

- `LocalisationEntry`: language-qualified localisation key and value.
- `Sprite`, `Portrait`, `Texture`, `Sound`, `MusicTrack`, and `RadioStation`.
- `InterfaceDefinition`, `Font`, `Particle`, and other presentation registries.
- `ScriptedGui`, `MapMode`, `GraphicalEntity`, `Mesh`, `Animation`, and material
  registrations where their formats and lookup rules are known.

Asset entities distinguish logical names used by script from physical files.
Several logical sprites may reference one texture, and one entity may select
different localisation entries by language.

## Authoring and workspace objects

Authoring objects describe what a modder is working on rather than objects
loaded into a campaign. Initial authoring kinds include:

- `ModProject`: writable roots, descriptor pair, supported game version,
  dependencies, replacement paths, and packaging policy;
- `Playset` and `LoadPlan`: ordered base-game, DLC, dependency, and active-mod
  layers used to construct a workspace snapshot;
- `GameVersionBaseline`: installation identity and extracted profile evidence
  against which compatibility is evaluated;
- `FeaturePlan`: a task-level collection of required and optional declarations,
  assets, localisation, edits, and validation checks;
- `ChangeSet`: a previewable group of semantic intentions and resulting source
  edits, including incomplete work that has not yet been applied;
- `ValidationRun`: static checks or a game launch tied to exact source,
  installation, profile, and playset versions; and
- `CompatibilityPatch`: an author-declared relationship between mods whose
  combined effective view requires additional content.

These objects may reference semantic entities and source documents. They do not
participate in game identity, override resolution, or serialization unless an
explicit operation produces game content from them.

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

The model also represents non-pointer constraints that matter to authors:

- registry contracts: a specialization `requires matching facility`;
- placement: a declaration `must reside under` a virtual path;
- naming conventions: an entity `implicitly resolves` a localisation or asset
  key through a documented fallback chain;
- script context: a block `enters scope`, `expects scope`, or `provides context`;
- availability: content `requires DLC` or `requires game version`;
- loading: a declaration `overrides`, `merges with`, `shadows`, or `is excluded
  by replacement path`; and
- performance: a construct `may evaluate over` a large runtime set when that
  warning is supported by reliable evidence.

An implicit relationship records the convention and evidence that produced it.
It is never indistinguishable from an explicit token present in source.

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
- `LocalizationBundleConcept`: all language-qualified values and inferred
  fallbacks for a related set of keys, with completeness and encoding checks.
- `FeatureConcept`: the declarations, dependencies, assets, localisation,
  diagnostics, and source impact needed to understand or author a feature that
  crosses traditional entity boundaries.
- `ModProjectConcept`: descriptor, supported version, dependencies, replacement
  paths, content inventory, compatibility findings, and validation history.
- `CompatibilityConcept`: differences between game baselines or playsets and
  their impact on the active mod's copied, overridden, or referenced content.

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
- repair a missing or ambiguous reference;
- scaffold a mod, feature, or supporting declaration set;
- clone a coherent vanilla feature into the active mod with an explicit list of
  copied and still-referenced dependencies;
- add missing localisation or asset registrations;
- create or update a compatibility patch;
- compare and migrate content between game-version baselines;
- package a selected mod configuration; and
- request static validation or a game launch for a selected configuration.

Each content-changing intention is planned into a previewable `WorkspaceEdit`.
Validation and launch intentions produce a versioned `ValidationRun` instead.
Availability is represented by an entity, projection, or workspace capability
with a reason when disabled. This lets the same interface work for writable mod
content, read-only vanilla content, and uncertain semantic states without
offering unsafe actions.

## Extensibility

Entity kinds, property schemas, identity rules, relationship extractors,
resolution policies, display providers, and editor capabilities are registered
by a versioned `GameProfile`. Core types remain generic enough to support new
HoI4 releases and mod-defined conventions while typed schema packages provide
the rich behavior expected for known content.

Extensions may add knowledge, but may not bypass provenance, replace the
lossless source model, or claim certainty stronger than their evidence.

## Coverage and capability levels

Oxide does not limit recognition to subjects covered by official tutorials.
Every content-bearing registry discovered through a game profile or installation
inventory receives a declared support level:

1. lossless source access;
2. structural parsing and navigation;
3. generic declarations, registry identity, and duplicate detection;
4. inferred references and conventions with confidence metadata;
5. typed semantic entities and verified effective-value rules;
6. safe structured editing;
7. task-level generation and refactoring; and
8. runtime-verified behavior for a recorded validation run.

Known tutorial-backed systems may reach higher levels first, but an unfamiliar
or newly added registry must remain discoverable at levels 1 through 3 whenever
its files can be parsed structurally. The interface exposes the current level
and its limitations. A `GameProfile` may never imply runtime verification from
static schema knowledge alone.

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
