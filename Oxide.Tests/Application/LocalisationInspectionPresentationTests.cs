using Oxide.App.ViewModels;
using Oxide.Core.Semantics.Contributions;
using Oxide.Core.Workspaces;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Tests.Workspaces;

namespace Oxide.Tests.Application;

public sealed class LocalisationInspectionPresentationTests
{
    [Fact]
    public async Task State_view_follows_victory_point_name_reference_and_keeps_both_sources()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1050-Test.txt", "state={ id=1050 name=STATE_1050 }");
        fixture.WriteGameFile(
            "localisation/english/states_l_english.yml",
            "l_english:\n STATE_1050:0 \"$VICTORY_POINTS_1001$\"\n");
        fixture.WriteGameFile(
            "localisation/english/victory_points_l_english.yml",
            "l_english:\n VICTORY_POINTS_1001:0 \"Bengbu\"\n");
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));

        var state = new StateListItemViewModel(snapshot.Semantics.States[1050], snapshot, "english");

        Assert.Equal("Bengbu", state.DisplayName);
        Assert.Equal(["STATE_1050", "VICTORY_POINTS_1001"],
            state.LocalisationInspection.ReferenceChain.Select(reference => reference.Key));
        Assert.EndsWith("victory_points_l_english.yml", state.LocalisationSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task English_fallback_exposes_ordered_language_and_layer_resolution()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-Test.txt", "state={ id=1 name=STATE_1 }");
        fixture.WriteGameFile(
            "localisation/english/base_l_english.yml",
            "l_english:\n STATE_1:0 \"Base English\"\n");
        fixture.WriteModFile(
            "localisation/english/mod_l_english.yml",
            "l_english:\n STATE_1:0 \"Mod English\"\n");
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));

        var viewModel = new StateListItemViewModel(snapshot.Semantics.States[1], snapshot, "russian");
        var inspection = viewModel.LocalisationInspection;

        Assert.Equal("russian", inspection.RequestedLanguage);
        Assert.Equal("english", inspection.ResolvedLanguage);
        Assert.True(inspection.UsedEnglishFallback);
        Assert.Equal("English fallback used", inspection.FallbackStatus);
        Assert.Equal("Resolved", inspection.Outcome);
        Assert.Equal(["russian", "english"], inspection.Attempts.Select(attempt => attempt.Language));
        Assert.Equal("Missing", inspection.Attempts[0].Outcome);
        Assert.False(inspection.Attempts[0].HasContribution);
        var english = inspection.Attempts[1];
        Assert.Equal("Resolved", english.Outcome);
        Assert.True(english.HasContribution);
        Assert.Equal(2, english.Contribution!.ContributionCount);
        Assert.Equal(1, english.Contribution.EffectiveSource!.LayerPosition);
        Assert.Contains(english.Contribution.Contributions,
            contribution => contribution.Disposition is ContributionDisposition.Effective);
        Assert.Contains(english.Contribution.Contributions,
            contribution => contribution.Disposition is ContributionDisposition.Shadowed);
        Assert.Equal(english.Contribution.SemanticIdentity, inspection.SelectedContribution!.SemanticIdentity);
        Assert.Equal(english.Contribution.EffectiveSource, inspection.SelectedContribution.EffectiveSource);
    }

    [Fact]
    public async Task Ambiguous_requested_language_is_visible_and_does_not_claim_fallback()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-Test.txt", "state={ id=1 name=STATE_1 }");
        fixture.WriteGameFile(
            "localisation/english/name_l_english.yml",
            "l_english:\n STATE_1:0 \"English\"\n");
        fixture.WriteGameFile(
            "localisation/russian/first_l_russian.yml",
            "l_russian:\n STATE_1:0 \"Один\"\n");
        fixture.WriteGameFile(
            "localisation/russian/second_l_russian.yml",
            "l_russian:\n STATE_1:0 \"Два\"\n");
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));

        var inspection = new StateListItemViewModel(
            snapshot.Semantics.States[1],
            snapshot,
            "russian").LocalisationInspection;

        Assert.Equal("Ambiguous", inspection.Outcome);
        Assert.Equal("—", inspection.ResolvedLanguage);
        Assert.False(inspection.UsedEnglishFallback);
        Assert.Single(inspection.Attempts);
        Assert.Equal("russian", inspection.Attempts[0].Language);
        Assert.Equal("Ambiguous", inspection.Attempts[0].Outcome);
        Assert.Equal(ContributionOutcomePresentation.Ambiguous,
            inspection.SelectedContribution!.Outcome);
        Assert.Equal(2, inspection.SelectedContribution.ContributionCount);
        Assert.All(inspection.SelectedContribution.Contributions,
            contribution => Assert.Equal(ContributionDisposition.Ambiguous, contribution.Disposition));
    }
}
