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
        Assert.Throws<ArgumentException>(() => new DocumentEdit(target,
            [new TextChange(new TextSpan(3, 0), "a"), new TextChange(new TextSpan(3, 0), "b")]));
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

    [Fact]
    public async Task In_memory_preparation_supports_insertions_that_increase_line_count_and_preserves_bom()
    {
        using var fixture = new TemporaryWorkspace();
        const string source = "state = { id = 1 resources = { steel = 10 } }";
        var originalBytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(source)).ToArray();
        var path = fixture.WriteModFile("history/states/1-Test.txt", source);
        File.WriteAllBytes(path, originalBytes);
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));
        var document = Document(snapshot, "history/states/1-Test.txt");
        var insertionPoint = source.IndexOf(" } }", StringComparison.Ordinal);
        var change = new TextChange(new TextSpan(insertionPoint, 0), "\n    aluminium = 5");
        var edit = new WorkspaceEdit(
            WorkspaceEditId.Create(),
            snapshot.Version,
            "Add aluminium",
            [new DocumentEdit(EditCapabilityEvaluator.CreateTarget(snapshot, document.Id), [change])]);

        var prepared = InMemoryWorkspaceEditPreparer.Prepare(snapshot, edit);
        var result = Assert.Single(prepared.Documents);

        Assert.True(prepared.IsValid);
        Assert.Equal(document.Text!.LineCount + 1, result.UpdatedSource.LineCount);
        Assert.Equal(SourceEncoding.Utf8WithBom, result.UpdatedSource.Encoding);
        Assert.True(result.UpdatedSource.GetOriginalBytes().Span.StartsWith(Encoding.UTF8.GetPreamble()));
        Assert.Equal(
            "state = { id = 1 resources = { steel = 10\n    aluminium = 5 } }",
            result.UpdatedSource.Text);
        Assert.NotNull(result.SyntaxTree);
        Assert.NotEqual(edit.Documents[0].Target.ExpectedFingerprint, result.UpdatedFingerprint);
    }

    [Fact]
    public async Task In_memory_preparation_applies_multiple_snapshot_relative_changes_without_offset_drift()
    {
        using var fixture = new TemporaryWorkspace();
        const string source = "state = { id = 1 manpower = 10 state_category = rural }";
        fixture.WriteModFile("history/states/1-Test.txt", source);
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));
        var document = Document(snapshot, "history/states/1-Test.txt");
        var manpowerStart = source.IndexOf("10", StringComparison.Ordinal);
        var categoryStart = source.IndexOf("rural", StringComparison.Ordinal);
        var edit = new WorkspaceEdit(
            WorkspaceEditId.Create(),
            snapshot.Version,
            "Change two values",
            [new DocumentEdit(
                EditCapabilityEvaluator.CreateTarget(snapshot, document.Id),
                [
                    new TextChange(new TextSpan(manpowerStart, 2), "125000"),
                    new TextChange(new TextSpan(categoryStart, 5), "large_city"),
                ])]);

        var prepared = InMemoryWorkspaceEditPreparer.Prepare(snapshot, edit);

        Assert.True(prepared.IsValid);
        Assert.Equal(
            "state = { id = 1 manpower = 125000 state_category = large_city }",
            Assert.Single(prepared.Documents).UpdatedSource.Text);
    }

    [Fact]
    public async Task In_memory_preparation_preserves_exact_bytes_for_an_equivalent_replacement_and_reparses_localisation()
    {
        using var fixture = new TemporaryWorkspace();
        const string source = "\uFEFFl_english:\r\n STATE_1:0 \"A state\"\r\n";
        var path = fixture.WriteModFile("localisation/english/test_l_english.yml", source[1..]);
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(source[1..])).ToArray();
        File.WriteAllBytes(path, bytes);
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));
        var document = Document(snapshot, "localisation/english/test_l_english.yml");
        var valueStart = document.Text!.Text.IndexOf("A state", StringComparison.Ordinal);
        var edit = new WorkspaceEdit(
            WorkspaceEditId.Create(),
            snapshot.Version,
            "Equivalent localisation replacement",
            [new DocumentEdit(
                EditCapabilityEvaluator.CreateTarget(snapshot, document.Id),
                [new TextChange(new TextSpan(valueStart, "A state".Length), "A state")])]);

        var prepared = InMemoryWorkspaceEditPreparer.Prepare(snapshot, edit);
        var result = Assert.Single(prepared.Documents);

        Assert.True(prepared.IsValid);
        Assert.Equal(bytes, result.UpdatedSource.GetOriginalBytes().ToArray());
        Assert.Equal(NewlineKind.CarriageReturnLineFeed, result.UpdatedSource.Newlines);
        Assert.NotNull(result.LocalisationSyntaxTree);
        Assert.Null(result.SyntaxTree);
        Assert.Equal(edit.Documents[0].Target.ExpectedFingerprint, result.UpdatedFingerprint);
    }

    [Fact]
    public async Task In_memory_preparation_rejects_stale_fingerprint_out_of_range_and_malformed_results()
    {
        using var fixture = new TemporaryWorkspace();
        const string source = "state = { id = 1 manpower = 10 }";
        fixture.WriteModFile("history/states/1-Test.txt", source);
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));
        var document = Document(snapshot, "history/states/1-Test.txt");
        var target = EditCapabilityEvaluator.CreateTarget(snapshot, document.Id);

        var staleEdit = new WorkspaceEdit(
            WorkspaceEditId.Create(),
            snapshot.Version + 1,
            "Stale",
            [new DocumentEdit(Target(snapshot.Version + 1, "stale"), [new TextChange(new TextSpan(0, 0), "x")])]);
        var wrongFingerprintTarget = new DocumentEditTarget(
            snapshot.Version,
            target.DocumentId,
            target.LayerId,
            target.VirtualPath,
            target.PhysicalPath,
            DocumentContentFingerprint.Create("different"u8));
        var wrongFingerprintEdit = new WorkspaceEdit(
            WorkspaceEditId.Create(),
            snapshot.Version,
            "Wrong fingerprint",
            [new DocumentEdit(wrongFingerprintTarget, [new TextChange(new TextSpan(0, 0), "#")])]);
        var outOfRangeEdit = new WorkspaceEdit(
            WorkspaceEditId.Create(),
            snapshot.Version,
            "Out of range",
            [new DocumentEdit(target, [new TextChange(new TextSpan(source.Length + 1, 0), "x")])]);
        var malformedEdit = new WorkspaceEdit(
            WorkspaceEditId.Create(),
            snapshot.Version,
            "Malformed",
            [new DocumentEdit(target, [new TextChange(new TextSpan(source.Length - 1, 1), string.Empty)])]);

        Assert.Contains(InMemoryWorkspaceEditPreparer.Prepare(snapshot, staleEdit).Issues, issue => issue.Code == "OXIDE5001");
        Assert.Contains(
            Assert.Single(InMemoryWorkspaceEditPreparer.Prepare(snapshot, wrongFingerprintEdit).Documents).Issues,
            issue => issue.Code == "OXIDE5004");
        Assert.Contains(
            Assert.Single(InMemoryWorkspaceEditPreparer.Prepare(snapshot, outOfRangeEdit).Documents).Issues,
            issue => issue.Code == "OXIDE5006");
        Assert.Contains(
            Assert.Single(InMemoryWorkspaceEditPreparer.Prepare(snapshot, malformedEdit).Documents).Issues,
            issue => issue.Code == "OXIDE5008");
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
