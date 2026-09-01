using System.Diagnostics;
using Oxide.App.ViewModels;
using Oxide.Core.Workspaces;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Loading;
using Oxide.Core.Workspaces.Navigation;
using Oxide.Tests.Workspaces;
using Oxide.Syntax.Text;

namespace Oxide.Tests.Performance;

public sealed class WorkspacePerformanceScenarioTests
{
    [Fact]
    [Trait("Category", "PerformanceScenario")]
    public async Task Contribution_details_project_from_the_published_snapshot_without_reloading()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteGameFile("history/states/1-Base.txt", "state={ id=1 manpower=10 }");
        fixture.WriteModFile("history/states/1-Mod.txt", "state={ id=1 manpower=20 }");
        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(fixture.GameRoot, fixture.ModRoot));
        var state = snapshot.Semantics.States[1];
        var stopwatch = Stopwatch.StartNew();

        for (var iteration = 0; iteration < 2_000; iteration++)
        {
            var presentation = ContributionSetPresentation.Create(state, snapshot);
            Assert.Equal(2, presentation.ContributionCount);
            Assert.Single(presentation.Comparisons);
        }

        stopwatch.Stop();
        Assert.Same(snapshot, service.CurrentSnapshot);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"Contribution projection took {stopwatch.Elapsed.TotalMilliseconds:N0} ms.");
    }

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

    [Fact]
    [Trait("Category", "ExternalCorpus")]
    public async Task Extracted_corpus_largest_source_projection_remains_bounded()
    {
        var root = Environment.GetEnvironmentVariable("OXIDE_HOI4_CORPUS_ROOT");
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        using var service = new WorkspaceService();
        var snapshot = await service.OpenAsync(new WorkspaceConfiguration(root));
        var document = snapshot.Documents
            .Where(candidate => candidate.Text is not null)
            .MaxBy(candidate => candidate.Text!.Length)!;
        var offsets = new[] { 0, document.Text!.Length / 2, document.Text.Length };
        var stopwatch = Stopwatch.StartNew();

        foreach (var offset in offsets)
        {
            var target = new SourceNavigationTarget(
                snapshot.Version,
                document.Id,
                document.Layer.Id,
                document.VirtualPath,
                new TextSpan(offset, 0),
                "performance:largest-source",
                "Verify bounded external source presentation");
            var presentation = SourceViewerPresenter.Create(SourceNavigationResolver.Resolve(snapshot, target));
            Assert.InRange(
                presentation.Lines.Length,
                1,
                SourceViewerPresentationOptions.DefaultMaximumMaterializedLines);
            Assert.InRange(
                presentation.Highlights.Length,
                0,
                SourceViewerPresentationOptions.DefaultMaximumHighlightSpans);
            Assert.InRange(
                presentation.Diagnostics.Length,
                0,
                SourceViewerPresentationOptions.DefaultMaximumDiagnosticResults);
        }

        stopwatch.Stop();
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"Three largest-source projections took {stopwatch.Elapsed.TotalMilliseconds:N0} ms.");
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
        Assert.True(metrics.ClausewitzDocumentLoadingMilliseconds > 0);
        Assert.True(metrics.LocalisationDocumentLoadingMilliseconds >= 0);
        Assert.True(metrics.SemanticBuildingMilliseconds >= 0);
        Assert.True(metrics.LocalisationIndexingMilliseconds >= 0);
        Assert.True(metrics.TotalMilliseconds >= metrics.DocumentLoadingMilliseconds);
        Assert.True(metrics.DocumentsPerSecond > 0);
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
