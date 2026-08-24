using Oxide.Core.Semantics.Contributions;
using Oxide.Core.Semantics.Model;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Documents;
using Oxide.Syntax.Text;

namespace Oxide.Tests.Semantics;

public sealed class ContributionResolverTests
{
    [Fact]
    public void Single_valid_contribution_is_effective_with_exact_provenance()
    {
        var contribution = Valid("STATE:1", "base", BaseLayer, "history/states/1.txt", 10);

        var resolution = ContributionResolver.Resolve(
            new ContributionSet<string, string>("STATE:1", [contribution]),
            ContributionResolutionPolicy.LayeredOverride);

        Assert.Equal(ContributionResolutionKind.Effective, resolution.Kind);
        Assert.Equal(ContributionResolutionReasonKind.SingleCandidate, resolution.Reason.Kind);
        Assert.Same(contribution, resolution.EffectiveContribution);
        var resolved = Assert.Single(resolution.Contributions);
        Assert.Equal(ContributionDisposition.Effective, resolved.Disposition);
        Assert.Equal(contribution.Provenance, resolved.Contribution.Provenance);
    }

    [Fact]
    public void Layered_policy_selects_highest_contribution_and_preserves_every_loser()
    {
        var baseContribution = Valid("STATE:1", "base", BaseLayer, "history/states/base.txt", 10);
        var firstModContribution = Valid("STATE:1", "first", FirstModLayer, "history/states/first.txt", 20);
        var secondModContribution = Valid("STATE:1", "second", SecondModLayer, "history/states/second.txt", 30);
        var set = new ContributionSet<string, string>(
            "STATE:1",
            [secondModContribution, baseContribution, firstModContribution]);

        var resolution = ContributionResolver.Resolve(set, ContributionResolutionPolicy.LayeredOverride);

        Assert.Equal(ContributionResolutionKind.Effective, resolution.Kind);
        Assert.Equal(ContributionResolutionReasonKind.HigherLayerPrecedence, resolution.Reason.Kind);
        Assert.Same(secondModContribution, resolution.EffectiveContribution);
        Assert.Equal(["base", "first", "second"],
            resolution.Contributions.Select(candidate => candidate.Contribution.Declaration));
        Assert.Equal(2, resolution.ShadowedContributions.Length);
        Assert.Equal(3, resolution.Contributions.Length);
    }

    [Fact]
    public void Same_layer_duplicates_are_distinct_from_cross_layer_ambiguity()
    {
        var duplicateSet = new ContributionSet<string, string>("AAA",
        [
            Valid("AAA", "one", FirstModLayer, "common/country_tags/a.txt", 1),
            Valid("AAA", "two", FirstModLayer, "common/country_tags/b.txt", 2),
            Valid("AAA", "base", BaseLayer, "common/country_tags/base.txt", 3),
        ]);
        var layeredSet = new ContributionSet<string, string>("AAA",
        [
            Valid("AAA", "base", BaseLayer, "common/country_tags/base.txt", 1),
            Valid("AAA", "mod", FirstModLayer, "common/country_tags/mod.txt", 2),
        ]);

        var duplicate = ContributionResolver.Resolve(duplicateSet, ContributionResolutionPolicy.LayeredOverride);
        var ambiguous = ContributionResolver.Resolve(layeredSet, ContributionResolutionPolicy.Conservative);

        Assert.Equal(ContributionResolutionKind.DuplicateWithinLayer, duplicate.Kind);
        Assert.Equal(ContributionResolutionReasonKind.SameLayerDuplicate, duplicate.Reason.Kind);
        Assert.Null(duplicate.EffectiveContribution);
        Assert.Equal(2, duplicate.Contributions.Count(candidate =>
            candidate.Disposition is ContributionDisposition.Ambiguous));
        Assert.Single(duplicate.ShadowedContributions);

        Assert.Equal(ContributionResolutionKind.Ambiguous, ambiguous.Kind);
        Assert.Equal(ContributionResolutionReasonKind.PolicyDoesNotSelectAcrossLayers, ambiguous.Reason.Kind);
        Assert.Null(ambiguous.EffectiveContribution);
        Assert.All(ambiguous.Contributions, candidate =>
            Assert.Equal(ContributionDisposition.Ambiguous, candidate.Disposition));
    }

    [Fact]
    public void Invalid_highest_contribution_is_reported_without_falling_back()
    {
        var lower = Valid("STATE:1", "base", BaseLayer, "history/states/base.txt", 1);
        var winner = Invalid("STATE:1", "broken", FirstModLayer, "history/states/mod.txt", 2, "Missing state ID.");

        var resolution = ContributionResolver.Resolve(
            new ContributionSet<string, string>("STATE:1", [lower, winner]),
            ContributionResolutionPolicy.LayeredOverride);

        Assert.Equal(ContributionResolutionKind.InvalidWinner, resolution.Kind);
        Assert.Equal(ContributionResolutionReasonKind.HighestPrecedenceCandidateInvalid, resolution.Reason.Kind);
        Assert.Null(resolution.EffectiveContribution);
        Assert.Equal(ContributionDisposition.Shadowed, resolution.Contributions[0].Disposition);
        Assert.Equal(ContributionDisposition.Invalid, resolution.Contributions[1].Disposition);
        Assert.Contains("Missing state ID", resolution.Reason.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void Invalid_lower_contribution_remains_inspectable_beside_effective_winner()
    {
        var invalid = Invalid("STATE:1", "broken", BaseLayer, "history/states/base.txt", 1, "Malformed value.");
        var winner = Valid("STATE:1", "mod", FirstModLayer, "history/states/mod.txt", 2);

        var resolution = ContributionResolver.Resolve(
            new ContributionSet<string, string>("STATE:1", [winner, invalid]),
            ContributionResolutionPolicy.LayeredOverride);

        Assert.Same(winner, resolution.EffectiveContribution);
        var invalidResult = Assert.Single(resolution.InvalidContributions);
        Assert.Same(invalid, invalidResult.Contribution);
        Assert.Contains("Malformed value", invalidResult.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void Empty_set_has_a_distinct_missing_outcome()
    {
        var resolution = ContributionResolver.Resolve(
            new ContributionSet<string, string>("STATE:404", []),
            ContributionResolutionPolicy.LayeredOverride);

        Assert.Equal(ContributionResolutionKind.Missing, resolution.Kind);
        Assert.Equal(ContributionResolutionReasonKind.NoCandidates, resolution.Reason.Kind);
        Assert.Null(resolution.EffectiveContribution);
        Assert.Empty(resolution.Contributions);
    }

    [Fact]
    public void Contribution_identity_and_order_are_stable_across_input_order()
    {
        var first = Valid("STATE:1", "first", FirstModLayer, "history/states/z.txt", 20);
        var second = Valid("STATE:1", "second", FirstModLayer, "history/states/a.txt", 10);

        var forward = new ContributionSet<string, string>("STATE:1", [first, second]);
        var reverse = new ContributionSet<string, string>("STATE:1", [second, first]);

        Assert.Equal(forward.Contributions.Select(candidate => candidate.Id),
            reverse.Contributions.Select(candidate => candidate.Id));
        Assert.Equal(["second", "first"], forward.Contributions.Select(candidate => candidate.Declaration));
        Assert.Equal(ContributionId.Create(first.Provenance), first.Id);
    }

    [Fact]
    public void Contribution_set_rejects_mixed_identities_and_duplicate_contribution_ids()
    {
        var first = Valid("STATE:1", "first", BaseLayer, "history/states/1.txt", 1);
        var wrongIdentity = first with { Identity = "STATE:2" };
        var duplicateId = first with { Declaration = "duplicate" };

        Assert.Throws<ArgumentException>(() =>
            new ContributionSet<string, string>("STATE:1", [first, wrongIdentity]));
        Assert.Throws<ArgumentException>(() =>
            new ContributionSet<string, string>("STATE:1", [first, duplicateId]));
    }

    private static ContentLayer BaseLayer { get; } = ContentLayer.BaseGame("/game");

    private static ContentLayer FirstModLayer { get; } = ContentLayer.Mod("first", "First mod", "/first", 1);

    private static ContentLayer SecondModLayer { get; } = ContentLayer.Mod("second", "Second mod", "/second", 2);

    private static Contribution<string, string> Valid(
        string identity,
        string declaration,
        ContentLayer layer,
        string virtualPath,
        int spanStart) =>
        Contribution<string, string>.Valid(identity, declaration, Provenance(layer, virtualPath, spanStart));

    private static Contribution<string, string> Invalid(
        string identity,
        string declaration,
        ContentLayer layer,
        string virtualPath,
        int spanStart,
        string reason) =>
        Contribution<string, string>.Invalid(identity, declaration, Provenance(layer, virtualPath, spanStart), reason);

    private static SourceProvenance Provenance(ContentLayer layer, string virtualPath, int spanStart)
    {
        var path = new VirtualPath(virtualPath);
        return new SourceProvenance(
            DocumentId.Create(layer.Id, path),
            Path.Combine(layer.RootPath, virtualPath),
            layer,
            new TextSpan(spanStart, 5));
    }
}
