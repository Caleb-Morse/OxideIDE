using Oxide.Core.Workspaces;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Documents;
using Oxide.Core.Workspaces.Navigation;
using Oxide.Syntax.Text;

namespace Oxide.Tests.Workspaces;

public sealed class SourceNavigationResolverTests
{
    [Fact]
    public async Task Valid_target_resolves_exact_snapshot_text_layer_and_unicode_position()
    {
        using var fixture = new TemporaryWorkspace();
        var path = fixture.WriteGameFile(
            "history/states/1-Test.txt",
            "state={\r\n name=\"Åland\"\r\n}");
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        var document = Assert.Single(snapshot.Documents);
        var start = document.Text!.Text.IndexOf("Åland", StringComparison.Ordinal);
        var target = Target(snapshot.Version, document, new TextSpan(start, "Åland".Length));
        File.Delete(path);

        var resolution = SourceNavigationResolver.Resolve(snapshot, target);

        Assert.Equal(SourceNavigationStatus.Resolved, resolution.Status);
        Assert.True(resolution.IsResolved);
        Assert.Same(document, resolution.Document);
        Assert.Equal("Åland", resolution.Document!.Text!.GetText(resolution.Location!.Span));
        Assert.Equal(2, resolution.Location.StartLine);
        Assert.Equal(8, resolution.Location.StartColumn);
        Assert.Equal(document.Layer, resolution.Location.Layer);
        Assert.Equal(snapshot.Version, resolution.Location.SnapshotVersion);
    }

    [Fact]
    public async Task Snapshot_version_mismatch_is_stale_even_when_document_identity_still_exists()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-Test.txt", "state={ id=1 }");
        using var service = new WorkspaceService();
        var first = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        var target = Target(first.Version, first.Documents[0], new TextSpan(0, 5));
        var second = await service.ReloadAsync();

        var resolution = SourceNavigationResolver.Resolve(second, target);

        Assert.Equal(SourceNavigationStatus.SnapshotVersionMismatch, resolution.Status);
        Assert.Null(resolution.Document);
        Assert.Contains(first.Version.ToString(), resolution.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Document_layer_and_path_are_validated_in_addition_to_document_id()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-Test.txt", "state={ id=1 }");
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        var document = snapshot.Documents[0];
        var wrongLayer = new SourceNavigationTarget(
            snapshot.Version,
            document.Id,
            new ContentLayerId("other-layer"),
            document.VirtualPath,
            new TextSpan(0, 1),
            "State:global:1",
            "Test identity validation");

        var resolution = SourceNavigationResolver.Resolve(snapshot, wrongLayer);

        Assert.Equal(SourceNavigationStatus.SourceIdentityMismatch, resolution.Status);
        Assert.Same(document, resolution.Document);
        Assert.Null(resolution.Location);
    }

    [Fact]
    public async Task Missing_document_and_invalid_span_have_distinct_outcomes()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-Test.txt", "state={ id=1 }");
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        var document = snapshot.Documents[0];
        var missing = new SourceNavigationTarget(
            snapshot.Version,
            new DocumentId("missing"),
            document.Layer.Id,
            document.VirtualPath,
            new TextSpan(0, 1),
            "State:global:1",
            "Test missing document");
        var invalidSpan = Target(
            snapshot.Version,
            document,
            new TextSpan(document.Text!.Length, 1));

        var missingResult = SourceNavigationResolver.Resolve(snapshot, missing);
        var invalidResult = SourceNavigationResolver.Resolve(snapshot, invalidSpan);

        Assert.Equal(SourceNavigationStatus.DocumentNotFound, missingResult.Status);
        Assert.Equal(SourceNavigationStatus.InvalidSpan, invalidResult.Status);
    }

    [Fact]
    public async Task Failed_document_remains_distinguishable_and_diagnostic()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameBytes("history/states/1-Broken.txt", [0xFF, 0xFE, 0xFD]);
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        var document = Assert.Single(snapshot.Documents);
        var target = Target(snapshot.Version, document, new TextSpan(0, 0));

        var resolution = SourceNavigationResolver.Resolve(snapshot, target);

        Assert.Equal(SourceNavigationStatus.DocumentFailed, resolution.Status);
        Assert.Same(document, resolution.Document);
        Assert.Null(resolution.Location);
        Assert.NotEmpty(document.Diagnostics);
    }

    [Fact]
    public void Target_rejects_invalid_metadata_at_its_boundary()
    {
        var layer = new ContentLayerId("base-game");
        var path = new VirtualPath("history/states/1-Test.txt");

        Assert.Throws<ArgumentOutOfRangeException>(() => new SourceNavigationTarget(
            -1,
            DocumentId.Create(layer, path),
            layer,
            path,
            new TextSpan(0, 0),
            "State:global:1",
            "Test"));
        Assert.Throws<ArgumentException>(() => new SourceNavigationTarget(
            1,
            DocumentId.Create(layer, path),
            layer,
            path,
            new TextSpan(0, 0),
            " ",
            "Test"));
    }

    private static SourceNavigationTarget Target(
        long version,
        SourceDocument document,
        TextSpan span) =>
        new(
            version,
            document.Id,
            document.Layer.Id,
            document.VirtualPath,
            span,
            "State:global:1",
            "Inspect the state declaration");
}
