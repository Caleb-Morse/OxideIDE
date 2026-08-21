using Oxide.Core.Workspaces;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Documents;

namespace Oxide.Tests.Workspaces;

public sealed class RealWorkspaceSmokeTests
{
    [Fact]
    [Trait("Category", "Corpus")]
    public async Task Configured_hoi4_installation_builds_an_inspectable_workspace()
    {
        var root = Environment.GetEnvironmentVariable("OXIDE_HOI4_CORPUS_ROOT");
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(root));

        Assert.NotEmpty(snapshot.Documents);
        Assert.All(snapshot.Documents, document => Assert.Equal(DocumentLoadStatus.Loaded, document.LoadStatus));
        Assert.Equal(snapshot.Documents.Length, snapshot.DocumentsById.Count);
        Assert.All(snapshot.Documents, document =>
            Assert.Equal(DocumentContributionStatus.SoleCandidate, document.ContributionStatus));
    }
}
