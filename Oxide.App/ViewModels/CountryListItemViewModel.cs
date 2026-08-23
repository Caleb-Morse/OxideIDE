using System.Collections.Immutable;
using Oxide.Core.Semantics.Model;
using Oxide.Core.Semantics.Resolution;
using Oxide.Core.Workspaces.Snapshots;

namespace Oxide.App.ViewModels;

public sealed class CountryListItemViewModel
{
    public CountryListItemViewModel(
        CountryEntity entity,
        WorkspaceSnapshot snapshot,
        string language,
        bool allowEnglishFallback = true)
    {
        Entity = entity;
        Tag = entity.Id.LocalKey;
        var presentation = LocalisedNamePresentation.Create(
            snapshot.Semantics.LocalisationResolver.ResolveName(entity, language, allowEnglishFallback),
            Tag,
            snapshot);
        DisplayName = presentation.DisplayName;
        NameStatus = presentation.ResolutionStatus;
        LocalisationSource = presentation.SourcePath;
        LocalisationLocation = presentation.SourceLocation;
        LocalisationLayer = presentation.SourceLayer;
        ResolvedLanguage = presentation.SourceLanguage;
        DefinitionPath = entity.DefinitionPath?.Value ?? "No effective definition path";
        Status = entity.Status.ToString();
        DeclarationSummary = entity.Contributions.Length == 1
            ? entity.Contributions[0].Provenance.PhysicalPath
            : $"{entity.Contributions.Length} competing declarations";
        OwnedStateIds = snapshot.Semantics.States.Values
            .Where(state => state.Owner?.Resolution is ResolvedCountry owner && owner.Target.Id == entity.Id)
            .Select(state => int.Parse(state.Id.LocalKey, System.Globalization.CultureInfo.InvariantCulture))
            .Order()
            .ToImmutableArray();
        CoreStateIds = snapshot.Semantics.States.Values
            .Where(state => state.Cores.Any(core => core.Resolution is ResolvedCountry country && country.Target.Id == entity.Id))
            .Select(state => int.Parse(state.Id.LocalKey, System.Globalization.CultureInfo.InvariantCulture))
            .Order()
            .ToImmutableArray();
        OwnedStates = DescribeStates(OwnedStateIds);
        CoreStates = DescribeStates(CoreStateIds);
    }

    public CountryEntity Entity { get; }
    public string Tag { get; }
    public string DisplayName { get; }
    public string NameStatus { get; }
    public string DefinitionPath { get; }
    public string Status { get; }
    public string DeclarationSummary { get; }
    public string LocalisationSource { get; }
    public string LocalisationLocation { get; }
    public string LocalisationLayer { get; }
    public string ResolvedLanguage { get; }
    public ImmutableArray<int> OwnedStateIds { get; }
    public ImmutableArray<int> CoreStateIds { get; }
    public string OwnedStates { get; }
    public string CoreStates { get; }
    public string SearchText => $"{DisplayName} {Tag} {DefinitionPath} {DeclarationSummary}";

    private static string DescribeStates(ImmutableArray<int> states) => states.Length == 0
        ? "None"
        : string.Join(", ", states.Select(state => $"State {state}"));
}
