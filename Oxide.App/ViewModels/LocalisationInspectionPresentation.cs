using System.Collections.Immutable;
using Oxide.Core.Semantics.Contributions;
using Oxide.Core.Semantics.Identity;
using Oxide.Core.Semantics.Model;
using Oxide.Core.Semantics.Resolution;
using Oxide.Core.Workspaces.Snapshots;

namespace Oxide.App.ViewModels;

public sealed record LocalisationAttemptPresentation(
    int Order,
    string Language,
    string Outcome,
    string ResolutionReason,
    ContributionSetPresentation? Contribution)
{
    public string StepLabel => $"Step {Order}: {Language}";

    public bool HasContribution => Contribution is not null;

    public string AccessibleName => $"{StepLabel}; {Outcome}; {ResolutionReason}";
}

public sealed record LocalisationReferencePresentation(
    int Order,
    string Key,
    string Value,
    ContributionSetPresentation Contribution)
{
    public string StepLabel => $"Reference {Order}: {Key}";

    public string AccessibleName => $"{StepLabel}; value {Value}; {Contribution.EffectiveLayerLabel}";
}

public sealed record LocalisationInspectionPresentation(
    string Key,
    string RequestedLanguage,
    string ResolvedLanguage,
    bool UsedEnglishFallback,
    string FallbackStatus,
    string Outcome,
    string SelectionReason,
    ContributionSetPresentation? SelectedContribution,
    ImmutableArray<LocalisationAttemptPresentation> Attempts,
    ImmutableArray<LocalisationReferencePresentation> ReferenceChain)
{
    public static LocalisationInspectionPresentation Create(
        HumanReadableName name,
        string key,
        WorkspaceSnapshot snapshot)
    {
        if (name.Resolution is not LocalisationResolution resolution)
        {
            return new LocalisationInspectionPresentation(
                key,
                "—",
                "—",
                false,
                "English fallback was not used",
                name.Resolution is InvalidLocalisation ? "Invalid localisation request" : "No localisation key",
                "No language-qualified lookup was performed.",
                null,
                [],
                []);
        }

        var selected = resolution switch
        {
            ResolvedLocalisation resolved => ContributionSetPresentation.Create(
                new LocalisationEntry(
                    new LocalisationIdentity(resolved.ResolvedLanguage, resolved.Key),
                    resolved.ContributionResolution),
                snapshot),
            AmbiguousLocalisation ambiguous => ContributionSetPresentation.Create(
                new LocalisationEntry(
                    new LocalisationIdentity(ambiguous.CandidateLanguage, ambiguous.Key),
                    ambiguous.ContributionResolution),
                snapshot),
            InvalidLocalisationContribution invalid => ContributionSetPresentation.Create(
                new LocalisationEntry(
                    new LocalisationIdentity(invalid.CandidateLanguage, invalid.Key),
                    invalid.ContributionResolution),
                snapshot),
            _ => null,
        };
        var resolvedLanguage = resolution is ResolvedLocalisation resolvedResult
            ? resolvedResult.ResolvedLanguage.Value
            : "—";
        var usedFallback = resolution is ResolvedLocalisation { IsFallback: true };
        var selectionReason = resolution switch
        {
            ResolvedLocalisation resolved => resolved.SelectionReason,
            AmbiguousLocalisation ambiguous => ambiguous.ContributionResolution.Reason.Explanation,
            InvalidLocalisationContribution invalid => invalid.ContributionResolution.Reason.Explanation,
            MissingLocalisation => "No eligible declaration was found in the requested or fallback language.",
            _ => "The localisation lookup did not resolve.",
        };
        var outcome = resolution switch
        {
            ResolvedLocalisation => "Resolved",
            AmbiguousLocalisation => "Ambiguous",
            InvalidLocalisationContribution => "Invalid contribution",
            MissingLocalisation => "Missing",
            _ => "Unresolved",
        };
        var attempts = resolution.Attempts.Select((attempt, index) =>
        {
            if (attempt.ContributionResolution is not { } contributionResolution)
            {
                return new LocalisationAttemptPresentation(
                    index + 1,
                    attempt.CandidateLanguage.Value,
                    "Missing",
                    "No declaration exists for this language and key.",
                    null);
            }

            var contribution = ContributionSetPresentation.Create(
                new LocalisationEntry(
                    new LocalisationIdentity(attempt.CandidateLanguage, resolution.Key),
                    contributionResolution),
                snapshot);
            return new LocalisationAttemptPresentation(
                index + 1,
                attempt.CandidateLanguage.Value,
                DescribeOutcome(contributionResolution.Kind),
                contributionResolution.Reason.Explanation,
                contribution);
        }).ToImmutableArray();
        var references = resolution is ResolvedLocalisation resolvedReference
            ? resolvedReference.ReferenceChain.Select((step, index) => new LocalisationReferencePresentation(
                index + 1,
                step.Identity.Key.Value,
                step.Declaration.Value.Value,
                ContributionSetPresentation.Create(
                    new LocalisationEntry(step.Identity, step.ContributionResolution),
                    snapshot))).ToImmutableArray()
            : [];

        return new LocalisationInspectionPresentation(
            key,
            resolution.RequestedLanguage.Value,
            resolvedLanguage,
            usedFallback,
            usedFallback ? "English fallback used" : "English fallback not used",
            outcome,
            selectionReason,
            selected,
            attempts,
            references);
    }

    private static string DescribeOutcome(ContributionResolutionKind kind) => kind switch
    {
        ContributionResolutionKind.Effective => "Resolved",
        ContributionResolutionKind.DuplicateWithinLayer or ContributionResolutionKind.Ambiguous => "Ambiguous",
        ContributionResolutionKind.InvalidWinner => "Invalid contribution",
        ContributionResolutionKind.Missing => "Missing",
        _ => "Unsupported",
    };
}
