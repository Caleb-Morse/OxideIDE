using System.Collections.Immutable;
using System.Diagnostics;
using Oxide.Core.Semantics.Contributions;
using Oxide.Core.Semantics.Declarations;
using Oxide.Core.Semantics.Diagnostics;
using Oxide.Core.Semantics.Identity;
using Oxide.Core.Semantics.Model;
using Oxide.Core.Semantics.Resolution;
using Oxide.Core.Semantics.Snapshots;
using Oxide.Core.Workspaces.Documents;
using Oxide.Core.Workspaces.Refresh;
using Oxide.Syntax.Diagnostics;

namespace Oxide.Core.Semantics.Building;

internal static class SemanticBuilder
{
    public static SemanticBuildResult Build(ImmutableArray<SourceDocument> documents) =>
        BuildCore(
            documents,
            null,
            SemanticInvalidationPlan.Create([], rebuildAll: true));

    public static SemanticBuildResult BuildIncremental(
        ImmutableArray<SourceDocument> documents,
        SemanticSnapshot previousSnapshot,
        SemanticInvalidationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(previousSnapshot);
        ArgumentNullException.ThrowIfNull(plan);
        return BuildCore(documents, previousSnapshot, plan);
    }

    private static SemanticBuildResult BuildCore(
        ImmutableArray<SourceDocument> documents,
        SemanticSnapshot? previousSnapshot,
        SemanticInvalidationPlan plan)
    {
        var declarationInventory = BuildDeclarationInventory(documents, previousSnapshot, plan);
        var states = declarationInventory.States
            .Where(item => item.IsEligible)
            .Select(item => item.Declaration)
            .ToImmutableArray();
        var countries = declarationInventory.Countries
            .Where(item => item.IsEligible)
            .Select(item => item.Declaration)
            .ToImmutableArray();
        var strategicRegions = declarationInventory.StrategicRegions
            .Where(item => item.IsEligible)
            .Select(item => item.Declaration)
            .ToImmutableArray();
        var localisations = declarationInventory.Localisations
            .Where(item => item.IsEligible)
            .Select(item => item.Declaration)
            .ToImmutableArray();
        var diagnostics = declarationInventory.Diagnostics
            .Where(item => item.IsActive)
            .Select(item => item.Diagnostic)
            .ToImmutableArray()
            .ToBuilder();

        var countryEntities = plan.Rebuilds(SemanticRefreshDomain.Countries)
            ? BuildCountries(declarationInventory.Countries, diagnostics)
            : previousSnapshot!.Countries;
        if (!plan.Rebuilds(SemanticRefreshDomain.Countries))
        {
            diagnostics.AddRange(countryEntities.Values.SelectMany(entity => entity.Diagnostics));
        }

        var stateEntities = plan.Rebuilds(SemanticRefreshDomain.States)
            ? BuildStates(declarationInventory.States, countryEntities, diagnostics)
            : previousSnapshot!.States;
        if (!plan.Rebuilds(SemanticRefreshDomain.States))
        {
            diagnostics.AddRange(stateEntities.Values.SelectMany(entity => entity.Diagnostics));
        }

        var strategicRegionEntities = plan.Rebuilds(SemanticRefreshDomain.StrategicRegions)
            ? BuildStrategicRegions(declarationInventory.StrategicRegions, diagnostics)
            : previousSnapshot!.StrategicRegions;
        if (!plan.Rebuilds(SemanticRefreshDomain.StrategicRegions))
        {
            diagnostics.AddRange(strategicRegionEntities.Values.SelectMany(entity => entity.Diagnostics));
        }

        ProvinceStrategicRegionIndex provinceStrategicRegionIndex;
        if (plan.Rebuilds(SemanticRefreshDomain.ProvinceStrategicRegionIndex))
        {
            provinceStrategicRegionIndex = new ProvinceStrategicRegionIndex(strategicRegionEntities, diagnostics);
        }
        else
        {
            provinceStrategicRegionIndex = previousSnapshot!.ProvinceStrategicRegionIndex;
            diagnostics.AddRange(previousSnapshot.Diagnostics.Where(diagnostic => diagnostic.Code == "OXIDE4016"));
        }

        ImmutableDictionary<int, StateStrategicRegionMembership> stateStrategicRegionMemberships;
        if (plan.Rebuilds(SemanticRefreshDomain.StateStrategicRegionMemberships))
        {
            stateStrategicRegionMemberships = BuildStateStrategicRegionMemberships(
                stateEntities,
                provinceStrategicRegionIndex,
                diagnostics);
        }
        else
        {
            stateStrategicRegionMemberships = previousSnapshot!.StateStrategicRegionMemberships;
            diagnostics.AddRange(stateStrategicRegionMemberships.Values.SelectMany(membership => membership.Diagnostics));
        }

        var localisationMilliseconds = 0d;
        ImmutableDictionary<LocalisationIdentity, LocalisationEntry> localisationEntries;
        if (plan.Rebuilds(SemanticRefreshDomain.Localisations))
        {
            var localisationStart = Stopwatch.GetTimestamp();
            localisationEntries = BuildLocalisations(declarationInventory.Localisations, diagnostics);
            localisationMilliseconds = Stopwatch.GetElapsedTime(localisationStart).TotalMilliseconds;
        }
        else
        {
            localisationEntries = previousSnapshot!.Localisations;
            diagnostics.AddRange(previousSnapshot.Diagnostics.Where(diagnostic => diagnostic.Code == "OXIDE4009"));
        }

        var allDiagnostics = diagnostics.Distinct().ToImmutableArray();
        var snapshot = new SemanticSnapshot(
            states,
            countries,
            strategicRegions,
            localisations,
            declarationInventory,
            stateEntities,
            countryEntities,
            strategicRegionEntities,
            provinceStrategicRegionIndex,
            stateStrategicRegionMemberships,
            localisationEntries,
            allDiagnostics);
        return new SemanticBuildResult(
            snapshot,
            localisationMilliseconds,
            plan.RebuiltDomains,
            plan.ReusedDomains);
    }

    private static SemanticDeclarationInventory BuildDeclarationInventory(
        ImmutableArray<SourceDocument> documents,
        SemanticSnapshot? previousSnapshot,
        SemanticInvalidationPlan plan)
    {
        var allStates = ImmutableArray.CreateBuilder<DeclarationInventoryItem<StateDeclaration>>();
        var allCountries = ImmutableArray.CreateBuilder<DeclarationInventoryItem<CountryTagDeclaration>>();
        var allStrategicRegions = ImmutableArray.CreateBuilder<DeclarationInventoryItem<StrategicRegionDeclaration>>();
        var allLocalisations = ImmutableArray.CreateBuilder<DeclarationInventoryItem<LocalisationDeclaration>>();
        var allExtractionDiagnostics = ImmutableArray.CreateBuilder<SemanticDiagnosticInventoryItem>();

        if (previousSnapshot is not null)
        {
            if (!plan.Changes(ContentCategory.StateHistory))
            {
                allStates.AddRange(previousSnapshot.DeclarationInventory.States);
            }

            if (!plan.Changes(ContentCategory.CountryTags))
            {
                allCountries.AddRange(previousSnapshot.DeclarationInventory.Countries);
            }

            if (!plan.Changes(ContentCategory.StrategicRegion))
            {
                allStrategicRegions.AddRange(previousSnapshot.DeclarationInventory.StrategicRegions);
            }

            if (!plan.Changes(ContentCategory.Localisation))
            {
                allLocalisations.AddRange(previousSnapshot.DeclarationInventory.Localisations);
            }

            allExtractionDiagnostics.AddRange(previousSnapshot.DeclarationInventory.Diagnostics.Where(item =>
                SupportedContentProfile.TryClassify(item.Source.VirtualPath, out _, out var category)
                && !plan.Changes(category)));
        }

        foreach (var document in documents.Where(document => document.IsLoaded))
        {
            if (!plan.Changes(document.Participation.Category))
            {
                continue;
            }

            if (document.Participation.Category is ContentCategory.Localisation)
            {
                var extracted = LocalisationDeclarationExtractor.Extract(document);
                allLocalisations.AddRange(extracted.Select(declaration => Inventory(declaration, document)));
            }
            else if (document.Participation.Category is ContentCategory.StateHistory)
            {
                var result = StateDeclarationExtractor.Extract(document);
                allStates.AddRange(result.Declarations.Select(declaration => Inventory(declaration, document)));
                allExtractionDiagnostics.AddRange(result.Diagnostics.Select(diagnostic =>
                    InventoryDiagnostic(diagnostic, document)));
            }
            else if (document.Participation.Category is ContentCategory.CountryTags)
            {
                var result = CountryTagDeclarationExtractor.Extract(document);
                allCountries.AddRange(result.Declarations.Select(declaration => Inventory(declaration, document)));
                allExtractionDiagnostics.AddRange(result.Diagnostics.Select(diagnostic =>
                    InventoryDiagnostic(diagnostic, document)));
            }
            else if (document.Participation.Category is ContentCategory.StrategicRegion)
            {
                var result = StrategicRegionDeclarationExtractor.Extract(document);
                allStrategicRegions.AddRange(result.Declarations.Select(declaration => Inventory(declaration, document)));
                allExtractionDiagnostics.AddRange(result.Diagnostics.Select(diagnostic =>
                    InventoryDiagnostic(diagnostic, document)));
            }
        }

        return new SemanticDeclarationInventory(
            allStates.ToImmutable(),
            allCountries.ToImmutable(),
            allStrategicRegions.ToImmutable(),
            allLocalisations.ToImmutable(),
            allExtractionDiagnostics.ToImmutable());

        static DeclarationInventoryItem<TDeclaration> Inventory<TDeclaration>(
            TDeclaration declaration,
            SourceDocument document) =>
            new(declaration, document.SourceIdentity, document.Participation);

        static SemanticDiagnosticInventoryItem InventoryDiagnostic(
            SemanticDiagnostic diagnostic,
            SourceDocument document) =>
            new(diagnostic, document.SourceIdentity, document.Participation);
    }

    private static ImmutableDictionary<LocalisationIdentity, LocalisationEntry> BuildLocalisations(
        ImmutableArray<DeclarationInventoryItem<LocalisationDeclaration>> declarations,
        ImmutableArray<SemanticDiagnostic>.Builder diagnostics)
    {
        var entries = ImmutableDictionary.CreateBuilder<LocalisationIdentity, LocalisationEntry>();
        foreach (var group in declarations.GroupBy(item => item.Declaration.Identity))
        {
            var set = new ContributionSet<LocalisationIdentity, LocalisationDeclaration>(
                group.Key,
                group.Select(item => Contribution<LocalisationIdentity, LocalisationDeclaration>.FromInventory(
                    group.Key,
                    item,
                    item.Declaration.Provenance)));
            var resolution = ContributionResolver.Resolve(set, ContributionResolutionPolicy.LayeredOverride);
            entries.Add(group.Key, new LocalisationEntry(group.Key, resolution));

            if (resolution.Kind is ContributionResolutionKind.DuplicateWithinLayer
                or ContributionResolutionKind.Ambiguous)
            {
                var candidates = resolution.Contributions
                    .Where(candidate => candidate.Disposition is ContributionDisposition.Ambiguous)
                    .Select(candidate => candidate.Contribution.Provenance)
                    .ToImmutableArray();
                diagnostics.Add(new SemanticDiagnostic(
                    "OXIDE4009",
                    DiagnosticSeverity.Warning,
                    $"Localisation '{group.Key.Key}' for language '{group.Key.Language}' has duplicate declarations in the effective layer.",
                    null,
                    candidates[0],
                    candidates[1..]));
            }
        }

        return entries.ToImmutable();
    }

    private static ImmutableDictionary<string, CountryEntity> BuildCountries(
        ImmutableArray<DeclarationInventoryItem<CountryTagDeclaration>> declarations,
        ImmutableArray<SemanticDiagnostic>.Builder diagnostics)
    {
        var entities = ImmutableDictionary.CreateBuilder<string, CountryEntity>(StringComparer.Ordinal);
        foreach (var group in declarations.GroupBy(item => item.Declaration.NormalizedTag, StringComparer.Ordinal))
        {
            var id = EntityId.Country(group.Key);
            var set = new ContributionSet<EntityId, CountryTagDeclaration>(
                id,
                group.Select(item => Contribution<EntityId, CountryTagDeclaration>.FromInventory(
                    id,
                    item,
                    item.Declaration.Provenance)));
            var resolution = ContributionResolver.Resolve(set, ContributionResolutionPolicy.LayeredOverride);
            if (resolution.Kind is ContributionResolutionKind.Missing)
            {
                continue;
            }

            var entityDiagnostics = ImmutableArray.CreateBuilder<SemanticDiagnostic>();
            EffectiveValue<string>? effectivePath = null;
            var status = EntityStatus(resolution.Kind);

            if (resolution.EffectiveContribution is { } effective)
            {
                effectivePath = EffectiveValue<string>.FromContribution(
                    effective.Declaration.DefinitionPath,
                    resolution);
            }
            else if (resolution.Kind is ContributionResolutionKind.DuplicateWithinLayer
                or ContributionResolutionKind.Ambiguous)
            {
                var candidates = AmbiguousProvenance(resolution);
                var diagnostic = DuplicateIdentity(
                    id,
                    candidates);
                diagnostics.Add(diagnostic);
                entityDiagnostics.Add(diagnostic);
            }

            entities.Add(group.Key, new CountryEntity(
                id,
                resolution,
                status,
                effectivePath,
                entityDiagnostics.ToImmutable()));
        }

        return entities.ToImmutable();
    }

    private static ImmutableDictionary<int, StateEntity> BuildStates(
        ImmutableArray<DeclarationInventoryItem<StateDeclaration>> declarations,
        ImmutableDictionary<string, CountryEntity> countries,
        ImmutableArray<SemanticDiagnostic>.Builder diagnostics)
    {
        var entities = ImmutableDictionary.CreateBuilder<int, StateEntity>();
        foreach (var group in declarations
            .Where(item => item.Declaration.EntityId is not null)
            .GroupBy(item => item.Declaration.IdCandidates[0].Value))
        {
            var id = EntityId.State(group.Key);
            var set = new ContributionSet<EntityId, StateDeclaration>(
                id,
                group.Select(item => Contribution<EntityId, StateDeclaration>.FromInventory(
                    id,
                    item,
                    item.Declaration.Provenance)));
            var resolution = ContributionResolver.Resolve(set, ContributionResolutionPolicy.LayeredOverride);
            if (resolution.Kind is ContributionResolutionKind.Missing)
            {
                continue;
            }

            var entityDiagnostics = diagnostics
                .Where(diagnostic => diagnostic.EntityId == id)
                .ToImmutableArray()
                .ToBuilder();

            if (resolution.EffectiveContribution is not { } effective)
            {
                if (resolution.Kind is ContributionResolutionKind.DuplicateWithinLayer
                    or ContributionResolutionKind.Ambiguous)
                {
                    var duplicate = DuplicateIdentity(id, AmbiguousProvenance(resolution));
                    diagnostics.Add(duplicate);
                    entityDiagnostics.Add(duplicate);
                }

                entities.Add(group.Key, new StateEntity(
                    id,
                    resolution,
                    EntityStatus(resolution.Kind),
                    null,
                    null,
                    null,
                    ImmutableDictionary<string, EffectiveValue<decimal>>.Empty,
                    [],
                    null,
                    [],
                    entityDiagnostics.ToImmutable()));
                continue;
            }

            var declaration = effective.Declaration;
            var owner = declaration.OwnerCandidates.Length == 1
                ? ResolveCountry(declaration.OwnerCandidates[0], countries, id, diagnostics, entityDiagnostics)
                : null;
            var cores = declaration.CoreTags
                .Select(core => ResolveCountry(core, countries, id, diagnostics, entityDiagnostics))
                .ToImmutableArray();
            var resources = BuildEffectiveResources(declaration, resolution, id, diagnostics, entityDiagnostics);

            entities.Add(group.Key, new StateEntity(
                id,
                resolution,
                SemanticEntityStatus.Effective,
                Single(declaration.NameCandidates, resolution),
                Single(declaration.ManpowerCandidates, resolution),
                Single(declaration.StateCategoryCandidates, resolution),
                resources,
                declaration.Provinces
                    .Select(province => EffectiveValue<int>.FromContribution(province, resolution))
                    .ToImmutableArray(),
                owner,
                cores,
                entityDiagnostics.ToImmutable()));
        }

        return entities.ToImmutable();
    }

    private static ImmutableDictionary<int, StrategicRegionEntity> BuildStrategicRegions(
        ImmutableArray<DeclarationInventoryItem<StrategicRegionDeclaration>> declarations,
        ImmutableArray<SemanticDiagnostic>.Builder diagnostics)
    {
        var entities = ImmutableDictionary.CreateBuilder<int, StrategicRegionEntity>();
        foreach (var group in declarations
            .Where(item => item.Declaration.EntityId is not null)
            .GroupBy(item => item.Declaration.IdCandidates[0].Value))
        {
            var id = EntityId.StrategicRegion(group.Key);
            var set = new ContributionSet<EntityId, StrategicRegionDeclaration>(
                id,
                group.Select(item => Contribution<EntityId, StrategicRegionDeclaration>.FromInventory(
                    id,
                    item,
                    item.Declaration.Provenance)));
            var resolution = ContributionResolver.Resolve(set, ContributionResolutionPolicy.LayeredOverride);
            if (resolution.Kind is ContributionResolutionKind.Missing)
            {
                continue;
            }

            var entityDiagnostics = diagnostics
                .Where(diagnostic => diagnostic.EntityId == id)
                .ToImmutableArray()
                .ToBuilder();

            if (resolution.EffectiveContribution is not { } effective)
            {
                if (resolution.Kind is ContributionResolutionKind.DuplicateWithinLayer
                    or ContributionResolutionKind.Ambiguous)
                {
                    var duplicate = DuplicateIdentity(id, AmbiguousProvenance(resolution));
                    diagnostics.Add(duplicate);
                    entityDiagnostics.Add(duplicate);
                }

                entities.Add(group.Key, new StrategicRegionEntity(
                    id,
                    resolution,
                    EntityStatus(resolution.Kind),
                    null,
                    [],
                    entityDiagnostics.ToImmutable()));
                continue;
            }

            var declaration = effective.Declaration;
            entities.Add(group.Key, new StrategicRegionEntity(
                id,
                resolution,
                SemanticEntityStatus.Effective,
                Single(declaration.NameCandidates, resolution),
                declaration.Provinces
                    .Select(province => EffectiveValue<int>.FromContribution(province, resolution))
                    .ToImmutableArray(),
                entityDiagnostics.ToImmutable()));
        }

        return entities.ToImmutable();
    }

    private static ImmutableDictionary<int, StateStrategicRegionMembership> BuildStateStrategicRegionMemberships(
        ImmutableDictionary<int, StateEntity> states,
        ProvinceStrategicRegionIndex index,
        ImmutableArray<SemanticDiagnostic>.Builder diagnostics)
    {
        var memberships = ImmutableDictionary.CreateBuilder<int, StateStrategicRegionMembership>();
        foreach (var state in states.Values.OrderBy(state => state.Id.LocalKey, StringComparer.Ordinal))
        {
            var references = state.Provinces
                .Select(province => new ProvinceStrategicRegionReference(province, index.Resolve(province.Value)))
                .ToImmutableArray();
            var regions = references
                .Select(reference => reference.Resolution)
                .OfType<ResolvedProvinceStrategicRegion>()
                .Select(resolution => resolution.Region)
                .DistinctBy(region => region.Id)
                .OrderBy(region => region.Id.LocalKey, StringComparer.Ordinal)
                .ToImmutableArray();
            var status = ClassifyStateMembership(state, references, regions);
            var membershipDiagnostics = BuildMembershipDiagnostics(state, status, references, index.IsEmpty);
            diagnostics.AddRange(membershipDiagnostics);
            memberships.Add(int.Parse(state.Id.LocalKey, System.Globalization.CultureInfo.InvariantCulture),
                new StateStrategicRegionMembership(
                    state.Id,
                    status,
                    references,
                    regions,
                    membershipDiagnostics));
        }

        return memberships.ToImmutable();
    }

    private static StateStrategicRegionMembershipStatus ClassifyStateMembership(
        StateEntity state,
        ImmutableArray<ProvinceStrategicRegionReference> references,
        ImmutableArray<StrategicRegionEntity> regions)
    {
        if (state.Status is SemanticEntityStatus.Ambiguous
            || references.Any(reference => reference.Resolution is AmbiguousProvinceStrategicRegion))
        {
            return StateStrategicRegionMembershipStatus.Ambiguous;
        }

        if (references.Length == 0)
        {
            return StateStrategicRegionMembershipStatus.NoProvinces;
        }

        if (regions.Length > 1)
        {
            return StateStrategicRegionMembershipStatus.Split;
        }

        var missing = references.Count(reference => reference.Resolution is MissingProvinceStrategicRegion);
        if (regions.Length == 1 && missing > 0)
        {
            return StateStrategicRegionMembershipStatus.Partial;
        }

        return regions.Length == 1
            ? StateStrategicRegionMembershipStatus.SingleRegion
            : StateStrategicRegionMembershipStatus.Missing;
    }

    private static ImmutableArray<SemanticDiagnostic> BuildMembershipDiagnostics(
        StateEntity state,
        StateStrategicRegionMembershipStatus status,
        ImmutableArray<ProvinceStrategicRegionReference> references,
        bool indexIsEmpty)
    {
        if (status is StateStrategicRegionMembershipStatus.SingleRegion || indexIsEmpty)
        {
            return [];
        }

        var provenance = references.FirstOrDefault()?.StateProvince.Provenance
            ?? state.Contributions.FirstOrDefault()?.Provenance;
        var related = references
            .SelectMany(reference => reference.Resolution is AmbiguousProvinceStrategicRegion ambiguous
                ? ambiguous.Candidates.Select(candidate => candidate.Provenance)
                : [])
            .ToImmutableArray();
        var (code, severity, message) = status switch
        {
            StateStrategicRegionMembershipStatus.Ambiguous =>
                ("OXIDE4017", DiagnosticSeverity.Error, "State strategic-region membership is ambiguous."),
            StateStrategicRegionMembershipStatus.Split =>
                ("OXIDE4018", DiagnosticSeverity.Warning, "State provinces span several strategic regions."),
            StateStrategicRegionMembershipStatus.Partial =>
                ("OXIDE4019", DiagnosticSeverity.Warning, "Only some state provinces have strategic-region membership."),
            StateStrategicRegionMembershipStatus.Missing =>
                ("OXIDE4020", DiagnosticSeverity.Warning, "No state provinces have strategic-region membership."),
            StateStrategicRegionMembershipStatus.NoProvinces =>
                ("OXIDE4021", DiagnosticSeverity.Information, "State has no valid province candidates to resolve."),
            _ => throw new InvalidOperationException($"Unexpected membership status '{status}'."),
        };
        return [new SemanticDiagnostic(code, severity, message, state.Id, provenance, related)];
    }

    private static ImmutableDictionary<string, EffectiveValue<decimal>> BuildEffectiveResources(
        StateDeclaration declaration,
        ContributionResolution<EntityId, StateDeclaration> resolution,
        EntityId stateId,
        ImmutableArray<SemanticDiagnostic>.Builder diagnostics,
        ImmutableArray<SemanticDiagnostic>.Builder entityDiagnostics)
    {
        var resources = ImmutableDictionary.CreateBuilder<string, EffectiveValue<decimal>>(StringComparer.Ordinal);
        foreach (var group in declaration.Resources.GroupBy(resource => resource.Name, StringComparer.Ordinal))
        {
            var candidates = group.ToImmutableArray();
            if (candidates.Length == 1)
            {
                resources.Add(group.Key, EffectiveValue<decimal>.FromContribution(candidates[0].Value, resolution));
                continue;
            }

            var diagnostic = new SemanticDiagnostic(
                "OXIDE4005",
                DiagnosticSeverity.Warning,
                $"Resource '{group.Key}' is declared more than once; no effective value is selected.",
                stateId,
                candidates[0].Value.Provenance,
                candidates.Skip(1).Select(candidate => candidate.Value.Provenance).ToImmutableArray());
            diagnostics.Add(diagnostic);
            entityDiagnostics.Add(diagnostic);
        }

        return resources.ToImmutable();
    }

    private static CountryReference ResolveCountry(
        SourcedValue<string> candidate,
        ImmutableDictionary<string, CountryEntity> countries,
        EntityId stateId,
        ImmutableArray<SemanticDiagnostic>.Builder diagnostics,
        ImmutableArray<SemanticDiagnostic>.Builder entityDiagnostics)
    {
        CountryResolution resolution;
        SemanticDiagnostic? diagnostic = null;
        if (!IsCountryTag(candidate.Value))
        {
            resolution = new InvalidCountry("Country tag must contain three ASCII letters or digits.");
            diagnostic = ReferenceDiagnostic("OXIDE4008", $"Invalid country tag '{candidate.Value}'.");
        }
        else
        {
            var normalized = EntityId.NormalizeCountryTag(candidate.Value);
            if (!countries.TryGetValue(normalized, out var country))
            {
                resolution = new MissingCountry(normalized);
                diagnostic = ReferenceDiagnostic("OXIDE4006", $"Country tag '{candidate.Value}' could not be resolved.");
            }
            else if (country.Status is SemanticEntityStatus.Ambiguous)
            {
                var candidates = country.ContributionResolution.Contributions
                    .Where(contribution => contribution.Disposition is ContributionDisposition.Ambiguous)
                    .Select(contribution => contribution.Contribution.Declaration)
                    .ToImmutableArray();
                resolution = new AmbiguousCountry(
                    normalized,
                    candidates,
                    "Several country declarations in the effective layer have the same typed identity.");
                diagnostic = new SemanticDiagnostic(
                    "OXIDE4007",
                    DiagnosticSeverity.Error,
                    $"Country tag '{candidate.Value}' resolves to several declarations.",
                    stateId,
                    candidate.Provenance,
                    candidates.Select(contribution => contribution.Provenance).ToImmutableArray());
            }
            else if (country.Status is SemanticEntityStatus.Missing)
            {
                resolution = new MissingCountry(normalized);
                diagnostic = ReferenceDiagnostic(
                    "OXIDE4006",
                    $"Country tag '{candidate.Value}' has no participating declaration.");
            }
            else if (country.Status is SemanticEntityStatus.Invalid)
            {
                resolution = new InvalidCountry("The effective country declaration is invalid.");
                diagnostic = ReferenceDiagnostic(
                    "OXIDE4008",
                    $"Country tag '{candidate.Value}' resolves to an invalid declaration.");
            }
            else
            {
                resolution = new ResolvedCountry(country);
            }
        }

        if (diagnostic is not null)
        {
            diagnostics.Add(diagnostic);
            entityDiagnostics.Add(diagnostic);
        }

        return new CountryReference(candidate.Value, candidate.Provenance, resolution);

        SemanticDiagnostic ReferenceDiagnostic(string code, string message) => new(
            code,
            DiagnosticSeverity.Error,
            message,
            stateId,
            candidate.Provenance,
            []);
    }

    private static EffectiveValue<T>? Single<T, TDeclaration>(
        ImmutableArray<SourcedValue<T>> candidates,
        ContributionResolution<EntityId, TDeclaration> resolution) =>
        candidates.Length == 1 ? EffectiveValue<T>.FromContribution(candidates[0], resolution) : null;

    private static EffectiveValue<T>? Single<T>(ImmutableArray<SourcedValue<T>> candidates) =>
        candidates.Length == 1 ? EffectiveValue<T>.FromSingle(candidates[0]) : null;

    private static SemanticEntityStatus EntityStatus(ContributionResolutionKind kind) => kind switch
    {
        ContributionResolutionKind.Effective => SemanticEntityStatus.Effective,
        ContributionResolutionKind.DuplicateWithinLayer or ContributionResolutionKind.Ambiguous =>
            SemanticEntityStatus.Ambiguous,
        ContributionResolutionKind.Missing => SemanticEntityStatus.Missing,
        _ => SemanticEntityStatus.Invalid,
    };

    private static ImmutableArray<SourceProvenance> AmbiguousProvenance<TDeclaration>(
        ContributionResolution<EntityId, TDeclaration> resolution) =>
        resolution.Contributions
            .Where(contribution => contribution.Disposition is ContributionDisposition.Ambiguous)
            .Select(contribution => contribution.Contribution.Provenance)
            .ToImmutableArray();

    private static bool IsCountryTag(string text) =>
        text.Length == 3 && text.All(char.IsAsciiLetterOrDigit);

    private static SemanticDiagnostic DuplicateIdentity(
        EntityId entityId,
        ImmutableArray<SourceProvenance> candidates) =>
        new(
            "OXIDE4003",
            DiagnosticSeverity.Error,
            $"Entity '{entityId}' has multiple declarations and no verified resolution policy.",
            entityId,
            candidates[0],
            candidates[1..]);
}
