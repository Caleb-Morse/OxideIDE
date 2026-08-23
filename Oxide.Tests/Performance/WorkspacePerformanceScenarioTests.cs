using System.Diagnostics;
using Oxide.App.ViewModels;
using Oxide.Core.Workspaces;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Loading;
using Oxide.Tests.Workspaces;

namespace Oxide.Tests.Performance;

public sealed class WorkspacePerformanceScenarioTests
{
    [Fact]
    [Trait("Category", "PerformanceScenario")]
    public async Task Medium_synthetic_workspace_records_repeatable_stage_measurements()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("common/country_tags/00_countries.txt", "TST=\"countries/Test.txt\"");
        for (var id = 1; id <= 400; id++)
        {
            fixture.WriteGameFile(
                $"history/states/{id}-State.txt",
                $"state={{ id={id} manpower={id * 1000L} history={{ owner=TST }} provinces={{ {id} }} }}");
        }

        using var service = new WorkspaceService();
        var first = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot));
        var second = await service.ReloadAsync();

        AssertMetrics(first.LoadMetrics, 401);
        AssertMetrics(second.LoadMetrics, 401);
        Assert.Equal(first.Semantics.States.Count, second.Semantics.States.Count);
        Assert.Equal(400, second.Semantics.States.Count);
    }

    [Fact]
    public async Task Discovery_and_document_loading_execute_away_from_the_calling_thread()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-Test.txt", "state={ id=1 }");
        using var service = new WorkspaceService();
        using var workerReached = new ManualResetEventSlim();
        using var releaseWorker = new ManualResetEventSlim();
        var callingThreadId = Environment.CurrentManagedThreadId;
        var workerThreadId = callingThreadId;
        var progress = new InlineProgress<WorkspaceLoadProgress>(report =>
        {
            if (report.Stage is WorkspaceLoadStage.Discovering && report.ProcessedDocuments == 0)
            {
                workerThreadId = Environment.CurrentManagedThreadId;
                workerReached.Set();
                releaseWorker.Wait(TimeSpan.FromSeconds(5));
            }
        });

        var opening = service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot), progress);
        try
        {
            Assert.True(workerReached.Wait(TimeSpan.FromSeconds(5)));
            Assert.NotEqual(callingThreadId, workerThreadId);
            Assert.False(opening.IsCompleted);
        }
        finally
        {
            releaseWorker.Set();
        }

        var snapshot = await opening;
        Assert.Single(snapshot.Documents);
    }

    [Fact]
    [Trait("Category", "ExternalCorpus")]
    public async Task Extracted_corpus_language_switching_is_bounded_and_does_not_reload()
    {
        var root = Environment.GetEnvironmentVariable("OXIDE_HOI4_CORPUS_ROOT");
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        using var service = new WorkspaceService();
        using var viewModel = new MainWindowViewModel(service) { GameRootPath = root };
        await viewModel.OpenWorkspaceAsync();
        var published = service.CurrentSnapshot;
        var stopwatch = Stopwatch.StartNew();

        foreach (var language in viewModel.AvailableLanguages)
        {
            await viewModel.ChangeLanguageAsync(language.Id);
        }

        stopwatch.Stop();
        Assert.Same(published, service.CurrentSnapshot);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"Language projection took {stopwatch.Elapsed.TotalMilliseconds:N0} ms.");
    }

    private static void AssertMetrics(WorkspaceLoadMetrics metrics, int expectedDocuments)
    {
        Assert.Equal(expectedDocuments, metrics.DocumentCount);
        Assert.Equal(expectedDocuments, metrics.LoadedDocumentCount);
        Assert.Equal(0, metrics.FailedDocumentCount);
        Assert.Equal(0, metrics.WorkspaceDiagnosticCount);
        Assert.Equal(0, metrics.SemanticDiagnosticCount);
        Assert.True(metrics.DiscoveryMilliseconds >= 0);
        Assert.True(metrics.DocumentLoadingMilliseconds > 0);
        Assert.True(metrics.SemanticBuildingMilliseconds >= 0);
        Assert.True(metrics.TotalMilliseconds >= metrics.DocumentLoadingMilliseconds);
        Assert.True(metrics.DocumentsPerSecond > 0);
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
