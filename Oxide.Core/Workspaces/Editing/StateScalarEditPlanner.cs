using System.Collections.Immutable;
using System.Globalization;
using Oxide.Core.Semantics.Building;
using Oxide.Core.Semantics.Contributions;
using Oxide.Core.Semantics.Declarations;
using Oxide.Core.Semantics.Model;
using Oxide.Core.Workspaces.Documents;
using Oxide.Core.Workspaces.Snapshots;
using Oxide.Syntax.Diagnostics;
using Oxide.Syntax.Lexing;
using Oxide.Syntax.Text;

namespace Oxide.Core.Workspaces.Editing;

public enum StateScalarProperty
{
    Manpower,
    StateCategory,
}

public sealed record StateScalarEditIntent
{
    public StateScalarEditIntent(int stateId, StateScalarProperty property, string desiredValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(desiredValue);
        StateId = stateId;
        Property = property;
        DesiredValue = desiredValue.Trim();
    }

    public int StateId { get; }
    public StateScalarProperty Property { get; }
    public string DesiredValue { get; }

    public static StateScalarEditIntent SetManpower(int stateId, long manpower) =>
        new(stateId, StateScalarProperty.Manpower, manpower.ToString(CultureInfo.InvariantCulture));

    public static StateScalarEditIntent SetStateCategory(int stateId, string stateCategory) =>
        new(stateId, StateScalarProperty.StateCategory, stateCategory);
}

public sealed record StateScalarEditPlan(
    StateScalarEditIntent Intent,
    EditCapability Capability,
    WorkspaceEdit? Edit,
    PreparedWorkspaceEdit? PreparedEdit,
    ImmutableArray<EditValidationIssue> Issues)
{
    public bool IsValid =>
        Capability.IsEditable &&
        Edit is not null &&
        PreparedEdit?.IsValid is true &&
        Issues.All(issue => issue.Severity is not DiagnosticSeverity.Error);
}

public static class StateScalarEditPlanner
{
    public static StateScalarEditPlan Plan(WorkspaceSnapshot snapshot, StateScalarEditIntent intent)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(intent);

        if (!TryNormalizeValue(intent, out var normalizedValue, out var invalidValueExplanation))
        {
            return Refused(intent, EditRefusalReason.UnsupportedOperation, invalidValueExplanation);
        }

        if (!snapshot.Semantics.States.TryGetValue(intent.StateId, out var state))
        {
            return Refused(intent, EditRefusalReason.MissingProvenance, $"State {intent.StateId} is not present.");
        }

        if (state.ContributionResolution.Kind is not ContributionResolutionKind.Effective ||
            state.EffectiveDeclaration is not { } declaration)
        {
            return Refused(
                intent,
                EditRefusalReason.AmbiguousDeclaration,
                $"State {intent.StateId} has no single effective declaration.");
        }

        var candidates = Candidates(declaration, intent.Property);
        if (candidates.Length == 0)
        {
            return Refused(
                intent,
                EditRefusalReason.MissingProvenance,
                $"State {intent.StateId} does not declare {DisplayName(intent.Property)} in its effective source.");
        }

        if (candidates.Length != 1)
        {
            return Refused(
                intent,
                EditRefusalReason.AmbiguousDeclaration,
                $"State {intent.StateId} declares {DisplayName(intent.Property)} more than once.");
        }

        if (AlreadyHasValue(declaration, intent.Property, normalizedValue))
        {
            return Refused(
                intent,
                EditRefusalReason.NoChangeRequired,
                $"State {intent.StateId} already has the requested {DisplayName(intent.Property)} value.");
        }

        var provenance = candidates[0];
        var capability = EditCapabilityEvaluator.AssessDocument(
            snapshot,
            snapshot.Version,
            provenance.DocumentId,
            hasExactProvenance: provenance.Span.Length > 0,
            isDeclarationUnambiguous: true,
            operationSupported: true);
        if (!capability.IsEditable)
        {
            return new StateScalarEditPlan(intent, capability, null, null, []);
        }

        var target = EditCapabilityEvaluator.CreateTarget(snapshot, provenance.DocumentId);
        var edit = new WorkspaceEdit(
            WorkspaceEditId.Create(),
            snapshot.Version,
            $"Set state {intent.StateId} {DisplayName(intent.Property)} to {normalizedValue}",
            [new DocumentEdit(target, [new TextChange(provenance.Span, normalizedValue)])]);
        var prepared = InMemoryWorkspaceEditPreparer.Prepare(snapshot, edit);
        var issues = ValidateIntent(snapshot, state.EffectiveDeclaration, intent, normalizedValue, prepared);

        return new StateScalarEditPlan(intent, capability, edit, prepared, issues);
    }

    private static ImmutableArray<SourceProvenance> Candidates(
        StateDeclaration declaration,
        StateScalarProperty property) =>
        property switch
        {
            StateScalarProperty.Manpower => declaration.ManpowerCandidates
                .Select(candidate => candidate.Provenance)
                .ToImmutableArray(),
            StateScalarProperty.StateCategory => declaration.StateCategoryCandidates
                .Select(candidate => candidate.Provenance)
                .ToImmutableArray(),
            _ => [],
        };

    private static ImmutableArray<EditValidationIssue> ValidateIntent(
        WorkspaceSnapshot snapshot,
        StateDeclaration originalDeclaration,
        StateScalarEditIntent intent,
        string normalizedValue,
        PreparedWorkspaceEdit prepared)
    {
        if (!prepared.IsValid || prepared.Documents.Length != 1)
        {
            return [Error("OXIDE5010", "The candidate edit did not pass in-memory source validation.")];
        }

        var preparedDocument = prepared.Documents[0];
        var originalDocument = snapshot.DocumentsById[originalDeclaration.DocumentId];
        var candidateDocument = originalDocument with
        {
            Text = preparedDocument.UpdatedSource,
            SyntaxTree = preparedDocument.SyntaxTree,
            LocalisationSyntaxTree = null,
            Diagnostics = [],
        };
        var extraction = StateDeclarationExtractor.Extract(candidateDocument);
        if (extraction.Diagnostics.Any(diagnostic => diagnostic.Severity is DiagnosticSeverity.Error))
        {
            return [Error("OXIDE5011", "The candidate source does not produce a valid state declaration.")];
        }

        var matches = extraction.Declarations
            .Where(declaration => declaration.EntityId?.LocalKey == intent.StateId.ToString(CultureInfo.InvariantCulture))
            .ToArray();
        if (matches.Length != 1)
        {
            return [Error("OXIDE5012", $"The candidate source does not contain exactly one state {intent.StateId} declaration.")];
        }

        var matchesIntent = intent.Property switch
        {
            StateScalarProperty.Manpower =>
                matches[0].ManpowerCandidates is [{ Value: var value }] &&
                value.ToString(CultureInfo.InvariantCulture) == normalizedValue,
            StateScalarProperty.StateCategory =>
                matches[0].StateCategoryCandidates is [{ Value: var value }] &&
                value == normalizedValue,
            _ => false,
        };

        return matchesIntent
            ? []
            : [Error("OXIDE5013", "The candidate source does not express the requested semantic value.")];
    }

    private static bool TryNormalizeValue(
        StateScalarEditIntent intent,
        out string normalizedValue,
        out string explanation)
    {
        normalizedValue = intent.DesiredValue;
        explanation = string.Empty;
        if (intent.Property is StateScalarProperty.Manpower)
        {
            if (long.TryParse(intent.DesiredValue, NumberStyles.None, CultureInfo.InvariantCulture, out var manpower) &&
                manpower >= 0)
            {
                normalizedValue = manpower.ToString(CultureInfo.InvariantCulture);
                return true;
            }

            explanation = "State manpower must be a non-negative integer.";
            return false;
        }

        var lexed = ClausewitzLexer.Lex(SourceText.From(intent.DesiredValue));
        var meaningfulTokens = lexed.Tokens
            .Where(token => !token.Kind.IsTrivia() && token.Kind is not SyntaxKind.EndOfFileToken)
            .ToArray();
        if (lexed.Diagnostics.IsEmpty &&
            meaningfulTokens is [{ Kind: SyntaxKind.IdentifierToken }] &&
            meaningfulTokens[0].Text == intent.DesiredValue)
        {
            return true;
        }

        explanation = "State category must be one unquoted Clausewitz identifier.";
        return false;
    }

    private static bool AlreadyHasValue(
        StateDeclaration declaration,
        StateScalarProperty property,
        string normalizedValue) =>
        property switch
        {
            StateScalarProperty.Manpower =>
                declaration.ManpowerCandidates[0].Value.ToString(CultureInfo.InvariantCulture) == normalizedValue,
            StateScalarProperty.StateCategory => declaration.StateCategoryCandidates[0].Value == normalizedValue,
            _ => false,
        };

    private static string DisplayName(StateScalarProperty property) => property switch
    {
        StateScalarProperty.Manpower => "manpower",
        StateScalarProperty.StateCategory => "state category",
        _ => property.ToString(),
    };

    private static StateScalarEditPlan Refused(
        StateScalarEditIntent intent,
        EditRefusalReason reason,
        string explanation) =>
        new(intent, EditCapability.Refused(reason, explanation), null, null, []);

    private static EditValidationIssue Error(string code, string message) =>
        new(code, DiagnosticSeverity.Error, message);
}
