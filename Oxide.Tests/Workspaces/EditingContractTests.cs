using System.Collections.Immutable;
using System.Text;
using Oxide.Core.Workspaces;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Documents;
using Oxide.Core.Workspaces.Editing;
using Oxide.Syntax.Diagnostics;
using Oxide.Syntax.Text;

namespace Oxide.Tests.Workspaces;

public sealed class EditingContractTests
{
    [Fact]
    public void Fingerprints_are_stable_normalized_and_content_sensitive()
    {
        var first = DocumentContentFingerprint.Create("hello"u8);
        var same = new DocumentContentFingerprint(first.Value.ToUpperInvariant());
        var different = DocumentContentFingerprint.Create("hello!"u8);

        Assert.Equal(first, same);
        Assert.NotEqual(first, different);
        Assert.Equal(64, first.Value.Length);
        Assert.Throws<ArgumentException>(() => new DocumentContentFingerprint("not-a-hash"));
    }

    [Fact]
    public void Document_and_workspace_edits_normalize_changes_and_reject_ambiguous_contracts()
    {
        var target = Target(snapshotVersion: 7, document: "one");
        var later = new TextChange(new TextSpan(10, 2), "twenty");
        var earlier = new TextChange(new TextSpan(2, 3), "one");
        var document = new DocumentEdit(target, [later, earlier]);
        var edit = new WorkspaceEdit(WorkspaceEditId.Create(), 7, "Change two scalar values", [document]);

        Assert.Equal(earlier, document.Changes[0]);
        Assert.Equal(later, document.Changes[1]);
        Assert.Equal(0, earlier.LengthDelta);
        Assert.Equal(4, later.LengthDelta);
        Assert.Single(edit.Documents);
        Assert.Throws<ArgumentException>(() => new DocumentEdit(target, []));
        Assert.Throws<ArgumentException>(() => new DocumentEdit(target,
            [new TextChange(new TextSpan(1, 4), "a"), new TextChange(new TextSpan(3, 2), "b")]));
        Assert.Throws<ArgumentException>(() => new WorkspaceEdit(
            WorkspaceEditId.Create(),
            7,
            "Duplicate document",
            [document, document]));
        Assert.Throws<ArgumentException>(() => new WorkspaceEdit(
            WorkspaceEditId.Create(),
            8,
            "Stale document target",
            [document]));
    }

    [Fact]
    public async Task Capability_requires_current_exact_unambiguous_writable_and_well_formed_source()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-Base.txt", "state={ id=1 manpower=10 }");
        fixture.WriteModFile("history/states/2-Mod.txt", "state={ id=2 manpower=20 }");
        fixture.WriteModFile("history/states/3-Broken.txt", "state={ id=3 bad=\u0001 }");
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));
        var baseDocument = Document(snapshot, "history/states/1-Base.txt");
        var modDocument = Document(snapshot, "history/states/2-Mod.txt");
        var brokenDocument = Document(snapshot, "history/states/3-Broken.txt");

        var editable = Assess(snapshot, modDocument);
        var readOnly = Assess(snapshot, baseDocument);
        var stale = EditCapabilityEvaluator.AssessDocument(
            snapshot, snapshot.Version + 1, modDocument.Id, true, true, true);
        var missingProvenance = EditCapabilityEvaluator.AssessDocument(
            snapshot, snapshot.Version, modDocument.Id, false, true, true);
        var ambiguous = EditCapabilityEvaluator.AssessDocument(
            snapshot, snapshot.Version, modDocument.Id, true, false, true);
        var unsupported = EditCapabilityEvaluator.AssessDocument(
            snapshot, snapshot.Version, modDocument.Id, true, true, false);
        var malformed = Assess(snapshot, brokenDocument);

        Assert.True(editable.IsEditable);
        Assert.Null(editable.RefusalReason);
        Assert.Equal(EditRefusalReason.ReadOnlyLayer, readOnly.RefusalReason);
        Assert.Equal(EditRefusalReason.StaleSnapshot, stale.RefusalReason);
        Assert.Equal(EditRefusalReason.MissingProvenance, missingProvenance.RefusalReason);
        Assert.Equal(EditRefusalReason.AmbiguousDeclaration, ambiguous.RefusalReason);
        Assert.Equal(EditRefusalReason.UnsupportedOperation, unsupported.RefusalReason);
        Assert.Equal(EditRefusalReason.MalformedSource, malformed.RefusalReason);
    }

    [Fact]
    public async Task Edit_targets_capture_exact_snapshot_source_identity_and_original_bytes()
    {
        using var fixture = new TemporaryWorkspace();
        const string source = "state={ id=1 name=\"Åland\" }";
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(source)).ToArray();
        var path = fixture.WriteModFile("history/states/1-Test.txt", source);
        File.WriteAllBytes(path, bytes);
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));
        var document = Document(snapshot, "history/states/1-Test.txt");

        var target = EditCapabilityEvaluator.CreateTarget(snapshot, document.Id);

        Assert.Equal(snapshot.Version, target.SnapshotVersion);
        Assert.Equal(document.Id, target.DocumentId);
        Assert.Equal(document.Layer.Id, target.LayerId);
        Assert.Equal(document.VirtualPath, target.VirtualPath);
        Assert.Equal(document.PhysicalPath, target.PhysicalPath);
        Assert.Equal(DocumentContentFingerprint.Create(bytes), target.ExpectedFingerprint);
    }

    [Fact]
    public void Preview_and_application_results_expose_validation_and_undo_state()
    {
        var target = Target(snapshotVersion: 1, document: "preview");
        var change = new TextChange(new TextSpan(0, 1), "2");
        var documentEdit = new DocumentEdit(target, [change]);
        var edit = new WorkspaceEdit(WorkspaceEditId.Create(), 1, "Preview", [documentEdit]);
        var warning = new EditValidationIssue("OXIDE5001", DiagnosticSeverity.Warning, "Review this change.");
        var preview = new WorkspaceEditPreview(
            edit,
            [new DocumentEditPreview(target, "1", "2", [change], [warning])],
            []);
        var undo = new WorkspaceEditUndoRecord(
            edit.Id,
            [new DocumentUndoEntry(target, ImmutableArray.Create<byte>(0x31), DocumentContentFingerprint.Create("2"u8))]);
        var result = new WorkspaceEditApplicationResult(WorkspaceEditApplicationStatus.Applied, "Applied.", undo);

        Assert.True(preview.IsValid);
        Assert.True(result.IsApplied);
        Assert.Same(undo, result.UndoRecord);
        Assert.Throws<ArgumentException>(() => new WorkspaceEditApplicationResult(
            WorkspaceEditApplicationStatus.Applied,
            "Missing undo state."));
        Assert.True(new WorkspaceEditUndoResult(WorkspaceEditUndoStatus.Restored, "Restored.").IsRestored);
        Assert.False(new WorkspaceEditPreview(
            edit,
            preview.Documents,
            [new EditValidationIssue("OXIDE5002", DiagnosticSeverity.Error, "Rejected.")]).IsValid);
    }

    private static EditCapability Assess(
        Oxide.Core.Workspaces.Snapshots.WorkspaceSnapshot snapshot,
        SourceDocument document) =>
        EditCapabilityEvaluator.AssessDocument(snapshot, snapshot.Version, document.Id, true, true, true);

    private static SourceDocument Document(
        Oxide.Core.Workspaces.Snapshots.WorkspaceSnapshot snapshot,
        string path) =>
        Assert.Single(snapshot.Documents, document =>
            string.Equals(document.VirtualPath.Value, path, StringComparison.OrdinalIgnoreCase));

    private static DocumentEditTarget Target(long snapshotVersion, string document)
    {
        var layerId = new ContentLayerId("active-mod");
        var virtualPath = new VirtualPath($"history/states/{document}.txt");
        return new DocumentEditTarget(
            snapshotVersion,
            DocumentId.Create(layerId, virtualPath),
            layerId,
            virtualPath,
            Path.Combine(Path.GetTempPath(), $"{document}.txt"),
            DocumentContentFingerprint.Create(Encoding.UTF8.GetBytes(document)));
    }
}
