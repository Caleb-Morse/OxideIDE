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
        ImmutableDictionary<int, StateEntity> states,
        ImmutableDictionary<string, CountryEntity> countries,
        ImmutableDictionary<LocalisationIdentity, LocalisationEntry> localisations,
        ImmutableArray<SemanticDiagnostic> diagnostics)
    {
        StateDeclarations = stateDeclarations;
        CountryDeclarations = countryDeclarations;
        StrategicRegionDeclarations = strategicRegionDeclarations;
        LocalisationDeclarations = localisationDeclarations;
        States = states;
        Countries = countries;
        Localisations = localisations;
        LocalisationResolver = new LocalisationResolver(localisations);
        Diagnostics = diagnostics;
        Entities = states.Values.Cast<ISemanticEntity>()
            .Concat(countries.Values)
            .ToImmutableDictionary(entity => entity.Id);
    }

    public ImmutableArray<StateDeclaration> StateDeclarations { get; }

    public ImmutableArray<CountryTagDeclaration> CountryDeclarations { get; }

    public ImmutableArray<StrategicRegionDeclaration> StrategicRegionDeclarations { get; }

    public ImmutableArray<LocalisationDeclaration> LocalisationDeclarations { get; }

    public ImmutableDictionary<int, StateEntity> States { get; }

    public ImmutableDictionary<string, CountryEntity> Countries { get; }

    public ImmutableDictionary<LocalisationIdentity, LocalisationEntry> Localisations { get; }

    public LocalisationResolver LocalisationResolver { get; }

    public ImmutableDictionary<EntityId, ISemanticEntity> Entities { get; }

    public ImmutableArray<SemanticDiagnostic> Diagnostics { get; }

    public static SemanticSnapshot Empty { get; } = new([], [], [], [], ImmutableDictionary<int, StateEntity>.Empty,
        ImmutableDictionary<string, CountryEntity>.Empty,
        ImmutableDictionary<LocalisationIdentity, LocalisationEntry>.Empty, []);
}
