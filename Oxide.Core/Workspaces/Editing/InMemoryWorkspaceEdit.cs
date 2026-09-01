using System.Collections.Immutable;
using System.Text;
using Oxide.Core.Workspaces.Documents;
using Oxide.Core.Workspaces.Snapshots;
using Oxide.Syntax.Diagnostics;
using Oxide.Syntax.Localisation;
using Oxide.Syntax.Parsing;
using Oxide.Syntax.Text;

namespace Oxide.Core.Workspaces.Editing;

public sealed record PreparedDocumentEdit(
    DocumentEdit Edit,
    SourceText OriginalSource,
    SourceText UpdatedSource,
    SyntaxTree? SyntaxTree,
    LocalisationSyntaxTree? LocalisationSyntaxTree,
    ImmutableArray<EditValidationIssue> Issues)
{
    public bool IsValid => Issues.All(issue => issue.Severity is not DiagnosticSeverity.Error);

    public DocumentContentFingerprint UpdatedFingerprint =>
        DocumentContentFingerprint.Create(UpdatedSource.GetOriginalBytes().Span);

    public DocumentEditPreview ToPreview() => new(
        Edit.Target,
        OriginalSource.Text,
        UpdatedSource.Text,
        Edit.Changes,
        Issues);
}

public sealed record PreparedWorkspaceEdit(
    WorkspaceEdit Edit,
    ImmutableArray<PreparedDocumentEdit> Documents,
    ImmutableArray<EditValidationIssue> Issues)
{
    public bool IsValid =>
        Documents.Length == Edit.Documents.Length &&
        Documents.All(document => document.IsValid) &&
        Issues.All(issue => issue.Severity is not DiagnosticSeverity.Error);

    public WorkspaceEditPreview ToPreview() =>
        new(Edit, Documents.Select(document => document.ToPreview()).ToImmutableArray(), Issues);
}

public static class InMemoryWorkspaceEditPreparer
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    public static PreparedWorkspaceEdit Prepare(
        WorkspaceSnapshot snapshot,
        WorkspaceEdit edit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(edit);
        cancellationToken.ThrowIfCancellationRequested();

        if (edit.SnapshotVersion != snapshot.Version)
        {
            return Rejected(edit, "OXIDE5001", "The workspace edit targets a stale snapshot.");
        }

        var prepared = ImmutableArray.CreateBuilder<PreparedDocumentEdit>(edit.Documents.Length);
        foreach (var documentEdit in edit.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = PrepareDocument(snapshot, documentEdit);
            if (result is null)
            {
                return Rejected(
                    edit,
                    "OXIDE5002",
                    $"Document '{documentEdit.Target.VirtualPath}' is not present in the target snapshot.");
            }

            prepared.Add(result);
        }

        return new PreparedWorkspaceEdit(edit, prepared.ToImmutable(), []);
    }

    private static PreparedDocumentEdit? PrepareDocument(WorkspaceSnapshot snapshot, DocumentEdit edit)
    {
        if (!snapshot.DocumentsById.TryGetValue(edit.Target.DocumentId, out var document))
        {
            return null;
        }

        var issues = ImmutableArray.CreateBuilder<EditValidationIssue>();
        var capability = EditCapabilityEvaluator.AssessDocument(
            snapshot,
            edit.Target.SnapshotVersion,
            edit.Target.DocumentId,
            hasExactProvenance: true,
            isDeclarationUnambiguous: true,
            operationSupported: true);
        if (!capability.IsEditable)
        {
            issues.Add(Error(
                "OXIDE5009",
                $"The target document is not editable ({capability.RefusalReason}): {capability.Explanation}"));
        }

        if (document.Layer.Id != edit.Target.LayerId ||
            document.VirtualPath != edit.Target.VirtualPath ||
            !string.Equals(document.PhysicalPath, edit.Target.PhysicalPath, StringComparison.Ordinal))
        {
            issues.Add(Error("OXIDE5003", "The document target does not match the snapshot source identity."));
        }

        if (document.Text is null)
        {
            issues.Add(Error("OXIDE5005", "The target document has no loaded source text."));
            return EmptyPrepared(edit, issues.ToImmutable());
        }

        var fingerprint = DocumentContentFingerprint.Create(document.Text.GetOriginalBytes().Span);
        if (fingerprint != edit.Target.ExpectedFingerprint)
        {
            issues.Add(Error("OXIDE5004", "The target fingerprint does not match the snapshot bytes."));
        }

        if (edit.Changes.Any(change => change.Span.End > document.Text.Length))
        {
            issues.Add(Error("OXIDE5006", "A text change falls outside the target document."));
            return new PreparedDocumentEdit(edit, document.Text, document.Text, null, null, issues.ToImmutable());
        }

        SourceText updatedSource;
        try
        {
            var updatedText = ApplyChanges(document.Text.Text, edit.Changes);
            updatedSource = SourceText.FromBytes(Encode(updatedText, document.Text.Encoding));
        }
        catch (EncoderFallbackException exception)
        {
            issues.Add(Error("OXIDE5007", $"Replacement text cannot be encoded safely: {exception.Message}"));
            return new PreparedDocumentEdit(edit, document.Text, document.Text, null, null, issues.ToImmutable());
        }

        SyntaxTree? syntaxTree = null;
        LocalisationSyntaxTree? localisationSyntaxTree = null;
        if (document.Kind is SourceDocumentKind.Clausewitz)
        {
            syntaxTree = ClausewitzParser.Parse(updatedSource);
            AddSyntaxErrors(syntaxTree.Diagnostics, issues);
        }
        else
        {
            localisationSyntaxTree = LocalisationParser.Parse(updatedSource);
            AddSyntaxErrors(localisationSyntaxTree.Diagnostics, issues);
        }

        return new PreparedDocumentEdit(
            edit,
            document.Text,
            updatedSource,
            syntaxTree,
            localisationSyntaxTree,
            issues.ToImmutable());
    }

    private static PreparedDocumentEdit EmptyPrepared(
        DocumentEdit edit,
        ImmutableArray<EditValidationIssue> issues)
    {
        var empty = SourceText.From(string.Empty);
        return new PreparedDocumentEdit(edit, empty, empty, null, null, issues);
    }

    private static string ApplyChanges(string original, ImmutableArray<TextChange> changes)
    {
        var builder = new StringBuilder(original);
        for (var index = changes.Length - 1; index >= 0; index--)
        {
            var change = changes[index];
            builder.Remove(change.Span.Start, change.Span.Length);
            builder.Insert(change.Span.Start, change.Replacement);
        }

        return builder.ToString();
    }

    private static byte[] Encode(string text, SourceEncoding encoding)
    {
        var content = StrictUtf8.GetBytes(text);
        if (encoding is SourceEncoding.Utf8)
        {
            return content;
        }

        var bytes = new byte[Utf8Bom.Length + content.Length];
        Utf8Bom.CopyTo(bytes, 0);
        content.CopyTo(bytes, Utf8Bom.Length);
        return bytes;
    }

    private static void AddSyntaxErrors(
        ImmutableArray<SyntaxDiagnostic> diagnostics,
        ImmutableArray<EditValidationIssue>.Builder issues)
    {
        foreach (var diagnostic in diagnostics.Where(item => item.Severity is DiagnosticSeverity.Error))
        {
            issues.Add(Error(
                "OXIDE5008",
                $"Updated source has {diagnostic.Code} at {diagnostic.Span}: {diagnostic.Message}"));
        }
    }

    private static EditValidationIssue Error(string code, string message) =>
        new(code, DiagnosticSeverity.Error, message);

    private static PreparedWorkspaceEdit Rejected(WorkspaceEdit edit, string code, string message) =>
        new(edit, [], [Error(code, message)]);
}
