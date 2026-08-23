using System.Collections.Immutable;
using System.Diagnostics;
using System.Security;
using System.Text;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Documents;
using Oxide.Core.Workspaces.Snapshots;
using Oxide.Syntax.Diagnostics;
using Oxide.Syntax.Localisation;
using Oxide.Syntax.Parsing;
using Oxide.Syntax.Text;
using Oxide.Core.Semantics.Building;

namespace Oxide.Core.Workspaces.Loading;

internal sealed class WorkspaceLoader
{
    private static readonly ImmutableArray<DiscoveryRule> DiscoveryRules =
    [
        new("history/states", "*.txt", SearchOption.TopDirectoryOnly, SourceDocumentKind.Clausewitz),
        new("common/country_tags", "*.txt", SearchOption.TopDirectoryOnly, SourceDocumentKind.Clausewitz),
        new("localisation", "*.yml", SearchOption.AllDirectories, SourceDocumentKind.Localisation),
    ];

    public Task<WorkspaceSnapshot> LoadAsync(
        long version,
        WorkspaceConfiguration configuration,
        IProgress<WorkspaceLoadProgress>? progress,
        CancellationToken cancellationToken)
    {
        return Task.Run(
            () => LoadCore(version, configuration, progress, cancellationToken),
            cancellationToken);
    }

    private static WorkspaceSnapshot LoadCore(
        long version,
        WorkspaceConfiguration configuration,
        IProgress<WorkspaceLoadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var totalStart = Stopwatch.GetTimestamp();
        var diagnostics = ImmutableArray.CreateBuilder<WorkspaceDiagnostic>();
        var layers = CreateLayers(configuration);
        progress?.Report(new WorkspaceLoadProgress(WorkspaceLoadStage.Discovering, 0, 0));

        var discoveryStart = Stopwatch.GetTimestamp();
        var candidates = DiscoverFiles(layers, diagnostics, cancellationToken);
        var discoveryElapsed = Stopwatch.GetElapsedTime(discoveryStart);
        progress?.Report(new WorkspaceLoadProgress(
            WorkspaceLoadStage.Discovering,
            candidates.Length,
            candidates.Length,
            ElapsedMilliseconds: discoveryElapsed.TotalMilliseconds,
            DiagnosticCount: diagnostics.Count));
        var documents = ImmutableArray.CreateBuilder<SourceDocument>(candidates.Length);
        var clausewitzDocumentLoadingMilliseconds = 0d;
        var localisationDocumentLoadingMilliseconds = 0d;

        var documentLoadingStart = Stopwatch.GetTimestamp();
        for (var index = 0; index < candidates.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = candidates[index];
            progress?.Report(new WorkspaceLoadProgress(
                WorkspaceLoadStage.LoadingDocuments,
                index,
                candidates.Length,
                candidate.PhysicalPath));
            var documentStart = Stopwatch.GetTimestamp();
            documents.Add(LoadDocument(candidate));
            var documentElapsed = Stopwatch.GetElapsedTime(documentStart).TotalMilliseconds;
            if (candidate.Kind is SourceDocumentKind.Localisation)
            {
                localisationDocumentLoadingMilliseconds += documentElapsed;
            }
            else
            {
                clausewitzDocumentLoadingMilliseconds += documentElapsed;
            }
        }

        var classifiedDocuments = ClassifyContributions(documents.ToImmutable());
        foreach (var document in classifiedDocuments)
        {
            diagnostics.AddRange(document.Diagnostics);
        }

        var documentLoadingElapsed = Stopwatch.GetElapsedTime(documentLoadingStart);
        progress?.Report(new WorkspaceLoadProgress(
            WorkspaceLoadStage.LoadingDocuments,
            candidates.Length,
            candidates.Length,
            ElapsedMilliseconds: documentLoadingElapsed.TotalMilliseconds,
            DiagnosticCount: diagnostics.Count));

        progress?.Report(new WorkspaceLoadProgress(
            WorkspaceLoadStage.BuildingSemantics,
            candidates.Length,
            candidates.Length));
        var semanticStart = Stopwatch.GetTimestamp();
        var semanticResult = SemanticBuilder.Build(classifiedDocuments);
        var semantics = semanticResult.Snapshot;
        var semanticElapsed = Stopwatch.GetElapsedTime(semanticStart);
        var totalElapsed = Stopwatch.GetElapsedTime(totalStart);
        var metrics = new WorkspaceLoadMetrics(
            classifiedDocuments.Length,
            classifiedDocuments.Count(document => document.IsLoaded),
            classifiedDocuments.Count(document => !document.IsLoaded),
            diagnostics.Count,
            semantics.Diagnostics.Length,
            discoveryElapsed.TotalMilliseconds,
            documentLoadingElapsed.TotalMilliseconds,
            clausewitzDocumentLoadingMilliseconds,
            localisationDocumentLoadingMilliseconds,
            semanticElapsed.TotalMilliseconds,
            semanticResult.LocalisationIndexingMilliseconds,
            totalElapsed.TotalMilliseconds);
        progress?.Report(new WorkspaceLoadProgress(
            WorkspaceLoadStage.BuildingSemantics,
            candidates.Length,
            candidates.Length,
            ElapsedMilliseconds: semanticElapsed.TotalMilliseconds,
            DiagnosticCount: semantics.Diagnostics.Length));
        return new WorkspaceSnapshot(
            version,
            configuration,
            layers,
            classifiedDocuments,
            diagnostics.ToImmutable(),
            semantics,
            metrics);
    }

    private static ImmutableArray<ContentLayer> CreateLayers(WorkspaceConfiguration configuration)
    {
        var layers = ImmutableArray.CreateBuilder<ContentLayer>();
        layers.Add(ContentLayer.BaseGame(configuration.GameRoot));
        if (configuration.ActiveModRoot is not null)
        {
            layers.Add(ContentLayer.ActiveMod(configuration.ActiveModRoot));
        }

        return layers.ToImmutable();
    }

    private static ImmutableArray<DocumentCandidate> DiscoverFiles(
        ImmutableArray<ContentLayer> layers,
        ImmutableArray<WorkspaceDiagnostic>.Builder diagnostics,
        CancellationToken cancellationToken)
    {
        var candidates = ImmutableArray.CreateBuilder<DocumentCandidate>();
        foreach (var layer in layers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(layer.RootPath))
            {
                diagnostics.Add(new WorkspaceDiagnostic(
                    "OXIDE3001",
                    DiagnosticSeverity.Error,
                    $"The {DescribeLayer(layer)} root does not exist.",
                    layer.RootPath));
                continue;
            }

            foreach (var rule in DiscoveryRules)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var directory = Path.Combine(layer.RootPath, rule.VirtualDirectory.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(directory))
                {
                    continue;
                }

                try
                {
                    foreach (var physicalPath in Directory.EnumerateFiles(directory, rule.Pattern, rule.SearchOption))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var relativePath = Path.GetRelativePath(layer.RootPath, physicalPath);
                        candidates.Add(new DocumentCandidate(
                            layer,
                            Path.GetFullPath(physicalPath),
                            new VirtualPath(relativePath),
                            rule.Kind));
                    }
                }
                catch (Exception exception) when (IsFileSystemException(exception))
                {
                    diagnostics.Add(new WorkspaceDiagnostic(
                        "OXIDE3002",
                        DiagnosticSeverity.Error,
                        $"Could not discover files in '{directory}': {exception.Message}",
                        directory));
                }
            }
        }

        return candidates
            .OrderBy(candidate => candidate.Layer.Position)
            .ThenBy(candidate => candidate.VirtualPath)
            .ThenBy(candidate => candidate.PhysicalPath, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static SourceDocument LoadDocument(DocumentCandidate candidate)
    {
        var documentId = DocumentId.Create(candidate.Layer.Id, candidate.VirtualPath);
        try
        {
            var source = SourceText.Load(candidate.PhysicalPath);
            var syntaxTree = candidate.Kind is SourceDocumentKind.Clausewitz
                ? ClausewitzParser.Parse(source)
                : null;
            var localisationSyntaxTree = candidate.Kind is SourceDocumentKind.Localisation
                ? LocalisationParser.Parse(source)
                : null;
            var syntaxDiagnostics = syntaxTree?.Diagnostics
                ?? localisationSyntaxTree?.Diagnostics
                ?? [];
            var diagnostics = syntaxDiagnostics
                .Select(diagnostic => new WorkspaceDiagnostic(
                    diagnostic.Code,
                    diagnostic.Severity,
                    diagnostic.Message,
                    candidate.PhysicalPath,
                    documentId,
                    diagnostic.Span))
                .ToImmutableArray();

            return new SourceDocument(
                documentId,
                candidate.Layer,
                candidate.PhysicalPath,
                candidate.VirtualPath,
                candidate.Kind,
                DocumentLoadStatus.Loaded,
                DocumentContributionStatus.SoleCandidate,
                source,
                syntaxTree,
                localisationSyntaxTree,
                diagnostics);
        }
        catch (Exception exception) when (IsFileSystemException(exception) || exception is DecoderFallbackException)
        {
            var diagnostic = new WorkspaceDiagnostic(
                "OXIDE3003",
                DiagnosticSeverity.Error,
                $"Could not load '{candidate.PhysicalPath}': {exception.Message}",
                candidate.PhysicalPath,
                documentId);

            return new SourceDocument(
                documentId,
                candidate.Layer,
                candidate.PhysicalPath,
                candidate.VirtualPath,
                candidate.Kind,
                DocumentLoadStatus.Failed,
                DocumentContributionStatus.SoleCandidate,
                null,
                null,
                null,
                [diagnostic]);
        }
    }

    private static ImmutableArray<SourceDocument> ClassifyContributions(
        ImmutableArray<SourceDocument> documents)
    {
        var collisions = documents
            .GroupBy(document => document.VirtualPath)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet();

        return documents
            .Select(document => collisions.Contains(document.VirtualPath)
                ? document with { ContributionStatus = DocumentContributionStatus.UnknownPrecedence }
                : document)
            .ToImmutableArray();
    }

    private static string DescribeLayer(ContentLayer layer) => layer.Kind switch
    {
        ContentLayerKind.BaseGame => "base-game",
        ContentLayerKind.ActiveMod => "active-mod",
        _ => "content-layer",
    };

    private static bool IsFileSystemException(Exception exception) => exception is
        IOException or
        UnauthorizedAccessException or
        SecurityException;

    private sealed record DiscoveryRule(
        string VirtualDirectory,
        string Pattern,
        SearchOption SearchOption,
        SourceDocumentKind Kind);

    private sealed record DocumentCandidate(
        ContentLayer Layer,
        string PhysicalPath,
        VirtualPath VirtualPath,
        SourceDocumentKind Kind);
}
