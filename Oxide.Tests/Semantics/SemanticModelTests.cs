using Oxide.Core.Semantics.Model;
using Oxide.Core.Semantics.Resolution;
using Oxide.Core.Workspaces;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Tests.Workspaces;

namespace Oxide.Tests.Semantics;

public sealed class SemanticModelTests
{
    [Fact]
    public async Task State_extraction_recognizes_the_initial_property_subset_with_provenance()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("common/country_tags/00_countries.txt", "USA=\"countries/USA.txt\"\nTEX=\"countries/Texas.txt\"");
        fixture.WriteGameFile("history/states/375-Texas.txt", """
            state = {
                id = 375
                name = "STATE_375"
                manpower = 5824712
                state_category = metropolis
                resources = { oil = 210 aluminium = 50 }
                history = { owner = USA add_core_of = USA add_core_of = TEX }
                provinces = { 805 1500 10337 }
            }
            """);
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        var state = snapshot.Semantics.States[375];

        Assert.Equal(SemanticEntityStatus.Effective, state.Status);
        Assert.Equal("STATE_375", state.Name?.Value);
        Assert.Equal(5_824_712, state.Manpower?.Value);
        Assert.Equal("metropolis", state.StateCategory?.Value);
        Assert.Equal(210m, state.Resources["oil"].Value);
        Assert.Equal(50m, state.Resources["aluminium"].Value);
        Assert.Equal([805, 1500, 10337], state.Provinces.Select(province => province.Value));
        Assert.Equal("\"STATE_375\"", state.Contributions[0].NameCandidates[0].OriginalText);
        Assert.Equal("\"STATE_375\"", snapshot.DocumentsById[state.Name!.Provenance.DocumentId].Text!.GetText(
            state.Name.Provenance.Span));
    }

    [Fact]
    public async Task Country_tags_are_normalized_but_original_spelling_is_retained()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("common/country_tags/00_countries.txt", "usa = \"countries/USA.txt\"");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        var country = snapshot.Semantics.Countries["USA"];

        Assert.Equal("usa", country.Contributions[0].OriginalTag);
        Assert.Equal("countries/USA.txt", country.DefinitionPath?.Value);
        Assert.Equal(SemanticEntityStatus.Effective, country.Status);
    }

    [Fact]
    public async Task Owner_and_core_references_resolve_to_country_entities()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("common/country_tags/00_countries.txt", "USA=\"countries/USA.txt\" TEX=\"countries/Texas.txt\"");
        fixture.WriteGameFile("history/states/1-Test.txt", "state={ id=1 history={ owner=USA add_core_of=TEX } }");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        var state = snapshot.Semantics.States[1];

        Assert.Equal("USA", Assert.IsType<ResolvedCountry>(state.Owner!.Resolution).Target.Id.LocalKey);
        Assert.Equal("TEX", Assert.IsType<ResolvedCountry>(Assert.Single(state.Cores).Resolution).Target.Id.LocalKey);
    }

    [Fact]
    public async Task Missing_and_invalid_country_references_remain_structured_and_diagnostic()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-Test.txt", "state={ id=1 history={ owner=ZZZ add_core_of=too_long } }");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        var state = snapshot.Semantics.States[1];

        Assert.IsType<MissingCountry>(state.Owner!.Resolution);
        Assert.IsType<InvalidCountry>(Assert.Single(state.Cores).Resolution);
        Assert.Contains(state.Diagnostics, diagnostic => diagnostic.Code == "OXIDE4006");
        Assert.Contains(state.Diagnostics, diagnostic => diagnostic.Code == "OXIDE4008");
    }

    [Fact]
    public async Task Duplicate_country_identity_makes_references_ambiguous_without_losing_candidates()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("common/country_tags/00_countries.txt", "USA=\"countries/USA.txt\"");
        fixture.WriteModFile("common/country_tags/00_mod.txt", "USA=\"countries/ModUSA.txt\"");
        fixture.WriteModFile("history/states/1-Test.txt", "state={ id=1 history={ owner=USA } }");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));
        var country = snapshot.Semantics.Countries["USA"];
        var resolution = Assert.IsType<AmbiguousCountry>(snapshot.Semantics.States[1].Owner!.Resolution);

        Assert.Equal(SemanticEntityStatus.Ambiguous, country.Status);
        Assert.Null(country.DefinitionPath);
        Assert.Equal(2, country.Contributions.Length);
        Assert.Equal(2, resolution.Candidates.Length);
        Assert.Contains(snapshot.Semantics.Diagnostics, diagnostic => diagnostic.Code == "OXIDE4003");
        Assert.Contains(snapshot.Semantics.Diagnostics, diagnostic => diagnostic.Code == "OXIDE4007");
    }

    [Fact]
    public async Task Duplicate_state_identity_retains_declarations_but_selects_no_effective_values()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-Base.txt", "state={ id=1 name=\"BASE\" }");
        fixture.WriteModFile("history/states/1-Mod.txt", "state={ id=1 name=\"MOD\" }");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));
        var state = snapshot.Semantics.States[1];

        Assert.Equal(SemanticEntityStatus.Ambiguous, state.Status);
        Assert.Equal(2, state.Contributions.Length);
        Assert.Null(state.Name);
        Assert.Contains(state.Diagnostics, diagnostic => diagnostic.Code == "OXIDE4003");
    }

    [Fact]
    public async Task Invalid_or_duplicate_state_ids_remain_as_declarations_and_diagnostics()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/Broken.txt", "state={ id=nope } state={ id=1 id=2 }");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));

        Assert.Equal(2, snapshot.Semantics.StateDeclarations.Length);
        Assert.Empty(snapshot.Semantics.States);
        Assert.All(snapshot.Semantics.StateDeclarations, declaration => Assert.Null(declaration.EntityId));
        Assert.Contains(snapshot.Semantics.Diagnostics, diagnostic => diagnostic.Code == "OXIDE4002");
        Assert.Contains(snapshot.Semantics.Diagnostics, diagnostic => diagnostic.Code == "OXIDE4005");
    }

    [Fact]
    public async Task Duplicate_recognized_property_has_candidates_but_no_effective_value()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-Test.txt", "state={ id=1 name=\"ONE\" name=\"TWO\" }");
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        var state = snapshot.Semantics.States[1];

        Assert.Equal(2, state.Contributions[0].NameCandidates.Length);
        Assert.Null(state.Name);
        Assert.Contains(state.Diagnostics, diagnostic => diagnostic.Code == "OXIDE4005");
    }

    [Fact]
    public async Task Malformed_semantic_values_remain_inspectable_without_partial_entities()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("common/country_tags/00_countries.txt", "TOOLONG=\"countries/Bad.txt\" USA>=\"countries/USA.txt\" GER=\"countries/Germany.txt\"");
        fixture.WriteGameFile("history/states/Broken.txt", """
            state = {
                id = 2147483648
                manpower = many
                resources = { oil = nope steel = { nested = value } }
                provinces = { 1 nope { nested = value } }
                history = { owner = USA add_core_of = TOOLONG }
            }
            state = missing_block
            state >= { id = 2 }
            """);
        using var service = new WorkspaceService();

        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));

        Assert.Empty(snapshot.Semantics.States);
        Assert.Single(snapshot.Semantics.StateDeclarations);
        Assert.Single(snapshot.Semantics.Countries);
        Assert.True(snapshot.Semantics.Countries.ContainsKey("GER"));
        Assert.Contains(snapshot.Semantics.Diagnostics, diagnostic => diagnostic.Code == "OXIDE4001");
        Assert.Contains(snapshot.Semantics.Diagnostics, diagnostic => diagnostic.Code == "OXIDE4002");
        Assert.Contains(snapshot.Semantics.Diagnostics, diagnostic => diagnostic.Code == "OXIDE4004");
    }
}
