using Oxide.Core.Semantics.Identity;
using Oxide.Core.Workspaces;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Documents;
using Oxide.Tests.Workspaces;

namespace Oxide.Tests.Semantics;

public sealed class StrategicRegionDeclarationTests
{
    [Fact]
    public async Task Extraction_recognizes_strategic_regions_with_lossless_provenance()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("map/strategicregions/1-North.txt", """
            strategic_region = {
                id = 1
                name = "STRATEGICREGION_1"
                provinces = { 10 20 30 }
                weather = { period = { between = { 0.0 30.11 } } }
            }
            """);
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        var declaration = Assert.Single(snapshot.Semantics.StrategicRegionDeclarations);

        Assert.Equal(EntityId.StrategicRegion(1), declaration.EntityId);
        Assert.Equal("\"STRATEGICREGION_1\"", Assert.Single(declaration.NameCandidates).OriginalText);
        Assert.Equal([10, 20, 30], declaration.Provinces.Select(province => province.Value));
        Assert.All(declaration.Provinces, province =>
        {
            var document = snapshot.DocumentsById[province.Provenance.DocumentId];
            Assert.NotNull(document.Text);
            Assert.Equal(province.OriginalText, document.Text!.GetText(province.Provenance.Span));
        });
        Assert.Empty(snapshot.Semantics.Diagnostics);
    }

    [Fact]
    public async Task Base_and_mod_strategic_region_files_are_both_discovered_and_extracted()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("map/strategicregions/1-Base.txt", "strategic_region={ id=1 provinces={ 1 } }");
        fixture.WriteModFile("map/strategicregions/2-Mod.txt", "strategic_region={ id=2 provinces={ 2 } }");
        fixture.WriteGameFile("map/strategicregions/nested/ignored.txt", "strategic_region={ id=3 provinces={ 3 } }");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));

        Assert.Equal(2, snapshot.Documents.Length);
        Assert.Equal([1, 2], snapshot.Semantics.StrategicRegionDeclarations
            .Select(declaration => declaration.IdCandidates[0].Value));
    }

    [Fact]
    public async Task Invalid_shapes_remain_declarations_and_produce_specific_diagnostics()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("map/strategicregions/Broken.txt", """
            strategic_region = missing_block
            strategic_region = {
                id = nope
                id = 2
                name = { invalid = yes }
                provinces = nope
            }
            strategic_region = {
                id = 3
                name = ONE
                name = TWO
                provinces = { 1 nope nested = value 1 }
                provinces = { 2 }
            }
            """);
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));

        Assert.Equal(2, snapshot.Semantics.StrategicRegionDeclarations.Length);
        Assert.Null(snapshot.Semantics.StrategicRegionDeclarations[0].EntityId);
        Assert.Equal(EntityId.StrategicRegion(3), snapshot.Semantics.StrategicRegionDeclarations[1].EntityId);
        Assert.Equal([1, 1, 2], snapshot.Semantics.StrategicRegionDeclarations[1].Provinces
            .Select(province => province.Value));
        Assert.Contains(snapshot.Semantics.Diagnostics, diagnostic => diagnostic.Code == "OXIDE4010");
        Assert.Contains(snapshot.Semantics.Diagnostics, diagnostic => diagnostic.Code == "OXIDE4011");
        Assert.Contains(snapshot.Semantics.Diagnostics, diagnostic => diagnostic.Code == "OXIDE4012");
        Assert.Contains(snapshot.Semantics.Diagnostics, diagnostic => diagnostic.Code == "OXIDE4013");
        Assert.Contains(snapshot.Semantics.Diagnostics, diagnostic => diagnostic.Code == "OXIDE4014");
        var duplicate = Assert.Single(snapshot.Semantics.Diagnostics, diagnostic => diagnostic.Code == "OXIDE4015");
        Assert.Single(duplicate.RelatedProvenance);
    }

    [Fact]
    public async Task File_without_a_top_level_declaration_is_diagnostic_and_does_not_abort_loading()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("map/strategicregions/Empty.txt", "weather={ period={} }");
        fixture.WriteGameFile("map/strategicregions/Valid.txt", "strategic_region={ id=4 provinces={ 40 } }");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));

        Assert.Equal(2, snapshot.Documents.Length);
        Assert.Single(snapshot.Semantics.StrategicRegionDeclarations);
        Assert.Contains(snapshot.Semantics.Diagnostics, diagnostic => diagnostic.Code == "OXIDE4010");
        Assert.Same(snapshot, service.CurrentSnapshot);
    }

    [Fact]
    public async Task Unreadable_region_file_remains_a_failed_document_beside_valid_declarations()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameBytes("map/strategicregions/Unreadable.txt", [0xC3, 0x28]);
        fixture.WriteGameFile("map/strategicregions/Valid.txt", "strategic_region={ id=5 provinces={ 50 } }");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));

        var failed = Assert.Single(snapshot.Documents, document => !document.IsLoaded);
        Assert.Equal(DocumentLoadStatus.Failed, failed.LoadStatus);
        Assert.Contains(failed.Diagnostics, diagnostic => diagnostic.Code == "OXIDE3003");
        Assert.Equal(EntityId.StrategicRegion(5),
            Assert.Single(snapshot.Semantics.StrategicRegionDeclarations).EntityId);
    }
}
