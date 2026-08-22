using System.Collections.Immutable;
using Oxide.Core.Semantics.Declarations;
using Oxide.Core.Semantics.Diagnostics;
using Oxide.Core.Semantics.Identity;
using Oxide.Core.Semantics.Model;
using Oxide.Core.Semantics.Resolution;
using Oxide.Core.Semantics.Snapshots;
using Oxide.Core.Workspaces.Documents;
using Oxide.Syntax.Diagnostics;

namespace Oxide.Core.Semantics.Building;

internal static class SemanticBuilder
{
    public static SemanticSnapshot Build(ImmutableArray<SourceDocument> documents)
    {
        var states = ImmutableArray.CreateBuilder<StateDeclaration>();
        var countries = ImmutableArray.CreateBuilder<CountryTagDeclaration>();
        var localisations = ImmutableArray.CreateBuilder<LocalisationDeclaration>();
        var diagnostics = ImmutableArray.CreateBuilder<SemanticDiagnostic>();

        foreach (var document in documents.Where(document => document.IsLoaded))
        {
            if (document.LocalisationSyntaxTree is not null)
            {
                localisations.AddRange(LocalisationDeclarationExtractor.Extract(document));
            }
            else if (document.VirtualPath.Value.StartsWith("history/states/", StringComparison.OrdinalIgnoreCase))
            {
                var result = StateDeclarationExtractor.Extract(document);
                states.AddRange(result.Declarations);
                diagnostics.AddRange(result.Diagnostics);
            }
            else if (document.VirtualPath.Value.StartsWith("common/country_tags/", StringComparison.OrdinalIgnoreCase))
            {
                var result = CountryTagDeclarationExtractor.Extract(document);
                countries.AddRange(result.Declarations);
                diagnostics.AddRange(result.Diagnostics);
            }
        }

        var countryEntities = BuildCountries(countries.ToImmutable(), diagnostics);
        var stateEntities = BuildStates(states.ToImmutable(), countryEntities, diagnostics);
        var localisationEntries = BuildLocalisations(localisations.ToImmutable(), diagnostics);
        var allDiagnostics = diagnostics.ToImmutable();

        return new SemanticSnapshot(
            states.ToImmutable(),
            countries.ToImmutable(),
            localisations.ToImmutable(),
            stateEntities,
            countryEntities,
            localisationEntries,
            allDiagnostics);
    }

    private static ImmutableDictionary<LocalisationIdentity, LocalisationEntry> BuildLocalisations(
        ImmutableArray<LocalisationDeclaration> declarations,
        ImmutableArray<SemanticDiagnostic>.Builder diagnostics)
    {
        var entries = ImmutableDictionary.CreateBuilder<LocalisationIdentity, LocalisationEntry>();
        foreach (var group in declarations.GroupBy(declaration => declaration.Identity))
        {
            var contributions = group
                .OrderBy(declaration => declaration.Provenance.Layer.Position)
                .ThenBy(declaration => declaration.Provenance.PhysicalPath, StringComparer.Ordinal)
                .ThenBy(declaration => declaration.Provenance.Span.Start)
                .ToImmutableArray();
            entries.Add(group.Key, new LocalisationEntry(group.Key, contributions));

            if (contributions.Length > 1)
            {
                diagnostics.Add(new SemanticDiagnostic(
                    "OXIDE4009",
                    DiagnosticSeverity.Warning,
                    $"Localisation '{group.Key.Key}' for language '{group.Key.Language}' has multiple declarations; resolution is ambiguous.",
                    null,
                    contributions[0].Provenance,
                    contributions.Skip(1).Select(declaration => declaration.Provenance).ToImmutableArray()));
            }
        }

        return entries.ToImmutable();
    }

    private static ImmutableDictionary<string, CountryEntity> BuildCountries(
        ImmutableArray<CountryTagDeclaration> declarations,
        ImmutableArray<SemanticDiagnostic>.Builder diagnostics)
    {
        var entities = ImmutableDictionary.CreateBuilder<string, CountryEntity>(StringComparer.Ordinal);
        foreach (var group in declarations.GroupBy(declaration => declaration.NormalizedTag, StringComparer.Ordinal))
        {
            var contributions = group
                .OrderBy(declaration => declaration.Provenance.Layer.Position)
                .ThenBy(declaration => declaration.Provenance.PhysicalPath, StringComparer.Ordinal)
                .ToImmutableArray();
            var entityDiagnostics = ImmutableArray.CreateBuilder<SemanticDiagnostic>();
            EffectiveValue<string>? effectivePath = null;
            SemanticEntityStatus status;

            if (contributions.Length == 1)
            {
                status = SemanticEntityStatus.Effective;
                effectivePath = EffectiveValue<string>.FromSingle(contributions[0].DefinitionPath);
            }
            else
            {
                status = SemanticEntityStatus.Ambiguous;
                var diagnostic = DuplicateIdentity(
                    contributions[0].EntityId,
                    contributions.Select(declaration => declaration.Provenance).ToImmutableArray());
                diagnostics.Add(diagnostic);
                entityDiagnostics.Add(diagnostic);
            }

            var id = EntityId.Country(group.Key);
            entities.Add(group.Key, new CountryEntity(
                id,
                contributions,
                status,
                effectivePath,
                entityDiagnostics.ToImmutable()));
        }

        return entities.ToImmutable();
    }

    private static ImmutableDictionary<int, StateEntity> BuildStates(
        ImmutableArray<StateDeclaration> declarations,
        ImmutableDictionary<string, CountryEntity> countries,
        ImmutableArray<SemanticDiagnostic>.Builder diagnostics)
    {
        var entities = ImmutableDictionary.CreateBuilder<int, StateEntity>();
        foreach (var group in declarations
            .Where(declaration => declaration.EntityId is not null)
            .GroupBy(declaration => declaration.IdCandidates[0].Value))
        {
            var contributions = group
                .OrderBy(declaration => declaration.Provenance.Layer.Position)
                .ThenBy(declaration => declaration.Provenance.PhysicalPath, StringComparer.Ordinal)
                .ToImmutableArray();
            var id = EntityId.State(group.Key);
            var entityDiagnostics = diagnostics
                .Where(diagnostic => diagnostic.EntityId == id)
                .ToImmutableArray()
                .ToBuilder();

            if (contributions.Length > 1)
            {
                var duplicate = DuplicateIdentity(
                    id,
                    contributions.Select(declaration => declaration.Provenance).ToImmutableArray());
                diagnostics.Add(duplicate);
                entityDiagnostics.Add(duplicate);
                entities.Add(group.Key, new StateEntity(
                    id,
                    contributions,
                    SemanticEntityStatus.Ambiguous,
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

            var declaration = contributions[0];
            var owner = declaration.OwnerCandidates.Length == 1
                ? ResolveCountry(declaration.OwnerCandidates[0], countries, id, diagnostics, entityDiagnostics)
                : null;
            var cores = declaration.CoreTags
                .Select(core => ResolveCountry(core, countries, id, diagnostics, entityDiagnostics))
                .ToImmutableArray();
            var resources = BuildEffectiveResources(declaration, id, diagnostics, entityDiagnostics);

            entities.Add(group.Key, new StateEntity(
                id,
                contributions,
                SemanticEntityStatus.Effective,
                Single(declaration.NameCandidates),
                Single(declaration.ManpowerCandidates),
                Single(declaration.StateCategoryCandidates),
                resources,
                declaration.Provinces.Select(EffectiveValue<int>.FromSingle).ToImmutableArray(),
                owner,
                cores,
                entityDiagnostics.ToImmutable()));
        }

        return entities.ToImmutable();
    }

    private static ImmutableDictionary<string, EffectiveValue<decimal>> BuildEffectiveResources(
        StateDeclaration declaration,
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
                resources.Add(group.Key, EffectiveValue<decimal>.FromSingle(candidates[0].Value));
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
                resolution = new AmbiguousCountry(
                    normalized,
                    country.Contributions,
                    "Several country declarations have the same typed identity.");
                diagnostic = new SemanticDiagnostic(
                    "OXIDE4007",
                    DiagnosticSeverity.Error,
                    $"Country tag '{candidate.Value}' resolves to several declarations.",
                    stateId,
                    candidate.Provenance,
                    country.Contributions.Select(contribution => contribution.Provenance).ToImmutableArray());
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

    private static EffectiveValue<T>? Single<T>(ImmutableArray<SourcedValue<T>> candidates) =>
        candidates.Length == 1 ? EffectiveValue<T>.FromSingle(candidates[0]) : null;

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
