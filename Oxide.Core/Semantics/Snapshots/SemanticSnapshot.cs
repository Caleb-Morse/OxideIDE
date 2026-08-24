using System.Collections.Immutable;
using Oxide.Core.Semantics.Declarations;
using Oxide.Core.Semantics.Diagnostics;
using Oxide.Core.Semantics.Identity;
using Oxide.Core.Semantics.Model;
using Oxide.Core.Semantics.Resolution;

namespace Oxide.Core.Semantics.Snapshots;

public sealed class SemanticSnapshot
{
    internal SemanticSnapshot(
        ImmutableArray<StateDeclaration> stateDeclarations,
        ImmutableArray<CountryTagDeclaration> countryDeclarations,
        ImmutableArray<StrategicRegionDeclaration> strategicRegionDeclarations,
        ImmutableArray<LocalisationDeclaration> localisationDeclarations,
        SemanticDeclarationInventory declarationInventory,
        ImmutableDictionary<int, StateEntity> states,
        ImmutableDictionary<string, CountryEntity> countries,
        ImmutableDictionary<int, StrategicRegionEntity> strategicRegions,
        ProvinceStrategicRegionIndex provinceStrategicRegionIndex,
        ImmutableDictionary<int, StateStrategicRegionMembership> stateStrategicRegionMemberships,
        ImmutableDictionary<LocalisationIdentity, LocalisationEntry> localisations,
        ImmutableArray<SemanticDiagnostic> diagnostics)
    {
        StateDeclarations = stateDeclarations;
        CountryDeclarations = countryDeclarations;
        StrategicRegionDeclarations = strategicRegionDeclarations;
        LocalisationDeclarations = localisationDeclarations;
        DeclarationInventory = declarationInventory;
        States = states;
        Countries = countries;
        StrategicRegions = strategicRegions;
        ProvinceStrategicRegionIndex = provinceStrategicRegionIndex;
        StateStrategicRegionMemberships = stateStrategicRegionMemberships;
        Localisations = localisations;
        LocalisationResolver = new LocalisationResolver(localisations);
        Diagnostics = diagnostics;
        Entities = states.Values.Cast<ISemanticEntity>()
            .Concat(countries.Values)
            .Concat(strategicRegions.Values)
            .ToImmutableDictionary(entity => entity.Id);
    }

    public ImmutableArray<StateDeclaration> StateDeclarations { get; }

    public ImmutableArray<CountryTagDeclaration> CountryDeclarations { get; }

    public ImmutableArray<StrategicRegionDeclaration> StrategicRegionDeclarations { get; }

    public ImmutableArray<LocalisationDeclaration> LocalisationDeclarations { get; }

    public SemanticDeclarationInventory DeclarationInventory { get; }

    public ImmutableDictionary<int, StateEntity> States { get; }

    public ImmutableDictionary<string, CountryEntity> Countries { get; }

    public ImmutableDictionary<int, StrategicRegionEntity> StrategicRegions { get; }

    public ProvinceStrategicRegionIndex ProvinceStrategicRegionIndex { get; }

    public ImmutableDictionary<int, StateStrategicRegionMembership> StateStrategicRegionMemberships { get; }

    public ImmutableDictionary<LocalisationIdentity, LocalisationEntry> Localisations { get; }

    public LocalisationResolver LocalisationResolver { get; }

    public ImmutableDictionary<EntityId, ISemanticEntity> Entities { get; }

    public ImmutableArray<SemanticDiagnostic> Diagnostics { get; }

    public static SemanticSnapshot Empty { get; } = new([], [], [], [], SemanticDeclarationInventory.Empty,
        ImmutableDictionary<int, StateEntity>.Empty,
        ImmutableDictionary<string, CountryEntity>.Empty,
        ImmutableDictionary<int, StrategicRegionEntity>.Empty,
        new ProvinceStrategicRegionIndex(ImmutableDictionary<int, StrategicRegionEntity>.Empty,
            ImmutableArray.CreateBuilder<SemanticDiagnostic>()),
        ImmutableDictionary<int, StateStrategicRegionMembership>.Empty,
        ImmutableDictionary<LocalisationIdentity, LocalisationEntry>.Empty, []);
}
