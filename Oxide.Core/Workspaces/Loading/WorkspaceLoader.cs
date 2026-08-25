using System.Collections.Immutable;
using System.Diagnostics;
using System.Security;
using System.Text;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Documents;
using Oxide.Core.Workspaces.Snapshots;
using Oxide.Core.Workspaces.Refresh;
using Oxide.Syntax.Diagnostics;
using Oxide.Syntax.Localisation;
using Oxide.Syntax.Parsing;
using Oxide.Syntax.Text;
using Oxide.Core.Semantics.Building;

namespace Oxide.Core.Workspaces.Loading;

internal sealed class WorkspaceLoader
{
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

    public Task<WorkspaceRefreshLoadResult> RefreshAsync(
        long version,
        WorkspaceSnapshot previousSnapshot,
        IncrementalRefreshRequest request,
        IProgress<WorkspaceLoadProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(previousSnapshot);
        ArgumentNullException.ThrowIfNull(request);
        return Task.Run(
            () => RefreshCore(version, previousSnapshot, request, progress, cancellationToken),
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
        var layers = ContentLayerMetadataLoader.Load(
            configuration.Layers.Where(layer => layer.IsEnabled),
            diagnostics,
            cancellationToken);
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

        var classifiedDocuments = ClassifyContributions(documents.ToImmutable(), layers);
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

    private static WorkspaceRefreshLoadResult RefreshCore(
        long version,
        WorkspaceSnapshot previousSnapshot,
        IncrementalRefreshRequest request,
        IProgress<WorkspaceLoadProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (request.BaseSnapshotVersion != previousSnapshot.Version)
        {
            throw new InvalidOperationException(
                $"Refresh request targets snapshot {request.BaseSnapshotVersion}, "
                + $"but the current snapshot is {previousSnapshot.Version}.");
        }

        if (request.RequiresFullRescan)
        {
            var fullSnapshot = LoadCore(
                version,
                previousSnapshot.Configuration,
                progress,
                cancellationToken);
            return new WorkspaceRefreshLoadResult(
                fullSnapshot,
                new WorkspaceRefreshMetrics(
                    request.Changes.RawEventCount,
                    request.Changes.Changes.Length,
                    fullSnapshot.Documents.Count(document =>
                        !previousSnapshot.DocumentsById.ContainsKey(document.Id)),
                    fullSnapshot.Documents.Count(document =>
                        previousSnapshot.DocumentsById.ContainsKey(document.Id)),
                    previousSnapshot.Documents.Count(document =>
                        !fullSnapshot.DocumentsById.ContainsKey(document.Id)),
                    0,
                    fullSnapshot.Documents.Length,
                    Enum.GetValues<SemanticRefreshDomain>().Length,
                    0,
                    true,
                    0,
                    fullSnapshot.LoadMetrics.DiscoveryMilliseconds,
                    fullSnapshot.LoadMetrics.DocumentLoadingMilliseconds,
                    fullSnapshot.LoadMetrics.SemanticBuildingMilliseconds,
                    0,
                    fullSnapshot.LoadMetrics.TotalMilliseconds)
                {
                    RebuiltSemanticDomains = Enum.GetValues<SemanticRefreshDomain>().ToImmutableArray(),
                    ReusedSemanticDomains = [],
                });
        }

        return RefreshDocuments(
            version,
            previousSnapshot,
            request,
            progress,
            cancellationToken);
    }

    private static WorkspaceRefreshLoadResult RefreshDocuments(
        long version,
        WorkspaceSnapshot previousSnapshot,
        IncrementalRefreshRequest request,
        IProgress<WorkspaceLoadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var totalStart = Stopwatch.GetTimestamp();
        var documentsById = previousSnapshot.DocumentsById.ToBuilder();
        var layersById = previousSnapshot.Layers.ToImmutableDictionary(layer => layer.Id);
        var changedDocumentIds = new HashSet<DocumentId>();
        var addedDocumentIds = new HashSet<DocumentId>();
        var removedDocumentIds = new HashSet<DocumentId>();
        var reparsedDocumentIds = new HashSet<DocumentId>();
        var clausewitzDocumentLoadingMilliseconds = 0d;
        var localisationDocumentLoadingMilliseconds = 0d;
        var documentLoadingStart = Stopwatch.GetTimestamp();
        progress?.Report(new WorkspaceLoadProgress(
            WorkspaceLoadStage.LoadingDocuments,
            0,
            request.Changes.Changes.Length));

        for (var index = 0; index < request.Changes.Changes.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var documentChange = request.Changes.Changes[index];
            var change = documentChange.Change;
            progress?.Report(new WorkspaceLoadProgress(
                WorkspaceLoadStage.LoadingDocuments,
                index,
                request.Changes.Changes.Length,
                change.Source.PhysicalPath));

            if (change.PreviousSource is not null)
            {
                ValidateRefreshSource(
                    change.PreviousSource,
                    documentChange,
                    layersById);
                if (!documentsById.ContainsKey(change.PreviousSource.DocumentId))
                {
                    throw new InvalidOperationException(
                        $"Refresh change references a document that is not in snapshot "
                        + $"{previousSnapshot.Version}: '{change.PreviousSource.VirtualPath}'.");
                }

                documentsById.Remove(change.PreviousSource.DocumentId);
                removedDocumentIds.Add(change.PreviousSource.DocumentId);
            }

            if (change.CurrentSource is null)
            {
                continue;
            }

            var classification = ValidateRefreshSource(
                change.CurrentSource,
                documentChange,
                layersById);
            var layer = layersById[change.CurrentSource.LayerId];

            var candidate = new DocumentCandidate(
                layer,
                change.CurrentSource.PhysicalPath,
                change.CurrentSource.VirtualPath,
                classification.DocumentKind!.Value,
                classification.Category!.Value);
            var documentStart = Stopwatch.GetTimestamp();
            var document = LoadDocument(candidate);
            var elapsed = Stopwatch.GetElapsedTime(documentStart).TotalMilliseconds;
            if (document.Kind is SourceDocumentKind.Localisation)
            {
                localisationDocumentLoadingMilliseconds += elapsed;
            }
            else
            {
                clausewitzDocumentLoadingMilliseconds += elapsed;
            }

            var existed = previousSnapshot.DocumentsById.ContainsKey(document.Id);
            documentsById[document.Id] = document;
            reparsedDocumentIds.Add(document.Id);
            if (existed)
            {
                changedDocumentIds.Add(document.Id);
                removedDocumentIds.Remove(document.Id);
            }
            else
            {
                addedDocumentIds.Add(document.Id);
            }
        }

        var documentLoadingElapsed = Stopwatch.GetElapsedTime(documentLoadingStart);
        var orderedDocuments = documentsById.Values
            .OrderBy(document => document.Layer.Position)
            .ThenBy(document => document.VirtualPath)
            .ThenBy(document => document.PhysicalPath, StringComparer.Ordinal)
            .ToImmutableArray();
        var classifiedDocuments = ClassifyContributions(orderedDocuments, previousSnapshot.Layers);
        var diagnostics = previousSnapshot.Diagnostics
            .Where(diagnostic => diagnostic.DocumentId is null)
            .Concat(classifiedDocuments.SelectMany(document => document.Diagnostics))
            .ToImmutableArray();
        progress?.Report(new WorkspaceLoadProgress(
            WorkspaceLoadStage.LoadingDocuments,
            request.Changes.Changes.Length,
            request.Changes.Changes.Length,
            ElapsedMilliseconds: documentLoadingElapsed.TotalMilliseconds,
            DiagnosticCount: diagnostics.Length));

        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(new WorkspaceLoadProgress(
            WorkspaceLoadStage.BuildingSemantics,
            classifiedDocuments.Length,
            classifiedDocuments.Length));
        var semanticStart = Stopwatch.GetTimestamp();
        var invalidationPlan = SemanticInvalidationPlan.Create(request.Changes.Changes);
        var semanticResult = SemanticBuilder.BuildIncremental(
            classifiedDocuments,
            previousSnapshot.Semantics,
            invalidationPlan);
        var semanticElapsed = Stopwatch.GetElapsedTime(semanticStart);
        var totalElapsed = Stopwatch.GetElapsedTime(totalStart);
        var loadMetrics = new WorkspaceLoadMetrics(
            classifiedDocuments.Length,
            classifiedDocuments.Count(document => document.IsLoaded),
            classifiedDocuments.Count(document => !document.IsLoaded),
            diagnostics.Length,
            semanticResult.Snapshot.Diagnostics.Length,
            0,
            documentLoadingElapsed.TotalMilliseconds,
            clausewitzDocumentLoadingMilliseconds,
            localisationDocumentLoadingMilliseconds,
            semanticElapsed.TotalMilliseconds,
            semanticResult.LocalisationIndexingMilliseconds,
            totalElapsed.TotalMilliseconds);
        var snapshot = new WorkspaceSnapshot(
            version,
            previousSnapshot.Configuration,
            previousSnapshot.Layers,
            classifiedDocuments,
            diagnostics,
            semanticResult.Snapshot,
            loadMetrics);
        var refreshMetrics = new WorkspaceRefreshMetrics(
            request.Changes.RawEventCount,
            request.Changes.Changes.Length,
            addedDocumentIds.Count,
            changedDocumentIds.Count,
            removedDocumentIds.Count,
            classifiedDocuments.Length - reparsedDocumentIds.Count,
            reparsedDocumentIds.Count,
            semanticResult.RebuiltDomains.Length,
            semanticResult.ReusedDomains.Length,
            false,
            0,
            0,
            documentLoadingElapsed.TotalMilliseconds,
            semanticElapsed.TotalMilliseconds,
            0,
            totalElapsed.TotalMilliseconds)
        {
            RebuiltSemanticDomains = semanticResult.RebuiltDomains,
            ReusedSemanticDomains = semanticResult.ReusedDomains,
        };
        return new WorkspaceRefreshLoadResult(snapshot, refreshMetrics);
    }

    private static WorkspaceChangePathResult ValidateRefreshSource(
        SourceIdentity source,
        DocumentChange documentChange,
        ImmutableDictionary<ContentLayerId, ContentLayer> layersById)
    {
        if (!layersById.TryGetValue(source.LayerId, out var layer))
        {
            throw new InvalidOperationException(
                $"Refresh change references unknown content layer '{source.LayerId}'.");
        }

        var classification = WorkspaceChangeClassifier.Classify(layer, source.PhysicalPath);
        if (!classification.IsSupported
            || classification.Source != source
            || classification.DocumentKind != documentChange.DocumentKind
            || classification.Category != documentChange.Category)
        {
            throw new InvalidOperationException(
                $"Refresh change no longer identifies the expected supported source: '{source.PhysicalPath}'.");
        }

        return classification;
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

            foreach (var rule in SupportedContentProfile.Rules)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var directory = Path.Combine(
                    layer.RootPath,
                    rule.Directory.Value.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(directory))
                {
                    continue;
                }

                try
                {
                    var searchOption = rule.IncludeSubdirectories
                        ? SearchOption.AllDirectories
                        : SearchOption.TopDirectoryOnly;
                    foreach (var physicalPath in Directory.EnumerateFiles(directory, "*", searchOption))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var relativePath = Path.GetRelativePath(layer.RootPath, physicalPath);
                        var virtualPath = new VirtualPath(relativePath);
                        if (!rule.Matches(virtualPath))
                        {
                            continue;
                        }

                        candidates.Add(new DocumentCandidate(
                            layer,
                            Path.GetFullPath(physicalPath),
                            virtualPath,
                            rule.DocumentKind,
                            rule.Category));
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
                DocumentParticipation.Participating(candidate.Category),
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
                DocumentParticipation.Participating(candidate.Category),
                null,
                null,
                null,
                [diagnostic]);
        }
    }

    private static ImmutableArray<SourceDocument> ClassifyContributions(
        ImmutableArray<SourceDocument> documents,
        ImmutableArray<ContentLayer> layers)
    {
        var highestDocumentByPath = documents
            .GroupBy(document => document.VirtualPath, VirtualPathComparer.GamePath)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(document => document.Layer.Position)
                    .ThenBy(document => document.PhysicalPath, StringComparer.Ordinal)
                    .First(),
                VirtualPathComparer.GamePath);
        return documents
            .Select(document =>
            {
                var participation = ClassifyParticipation(document, highestDocumentByPath, layers);
                return participation == document.Participation
                    ? document
                    : document with { Participation = participation };
            })
            .ToImmutableArray();
    }

    private static DocumentParticipation ClassifyParticipation(
        SourceDocument document,
        IReadOnlyDictionary<VirtualPath, SourceDocument> highestDocumentByPath,
        ImmutableArray<ContentLayer> layers)
    {
        var highestDocument = highestDocumentByPath[document.VirtualPath];
        var shadowingDocument = highestDocument.Layer.Position > document.Layer.Position
            ? highestDocument
            : null;
        var replacement = layers
            .Where(layer => layer.Position > document.Layer.Position)
            .SelectMany(layer => layer.ReplacementRules.Select(rule => (Layer: layer, Rule: rule)))
            .Where(candidate => IsWithin(document.VirtualPath, candidate.Rule.Path))
            .OrderByDescending(candidate => candidate.Layer.Position)
            .ThenByDescending(candidate => candidate.Rule.Path.Value.Length)
            .FirstOrDefault();
        if (shadowingDocument is not null
            && (replacement.Layer is null || shadowingDocument.Layer.Position >= replacement.Layer.Position))
        {
            return new DocumentParticipation(
                DocumentParticipationKind.ShadowedByHigherLayerPath,
                document.Participation.Category,
                $"The same virtual path is supplied by higher layer '{shadowingDocument.Layer.DisplayName}'.",
                shadowingDocument.Layer.Id,
                shadowingDocument.Id);
        }

        if (replacement.Layer is not null)
        {
            return new DocumentParticipation(
                DocumentParticipationKind.ExcludedByReplacementPath,
                document.Participation.Category,
                $"Higher layer '{replacement.Layer.DisplayName}' replaces '{replacement.Rule.Path}'.",
                replacement.Layer.Id,
                ReplacementRule: replacement.Rule);
        }

        return DocumentParticipation.Participating(document.Participation.Category);
    }

    private static bool IsWithin(VirtualPath path, VirtualPath directory) =>
        path.Value.Equals(directory.Value, StringComparison.OrdinalIgnoreCase)
        || path.Value.StartsWith($"{directory.Value}/", StringComparison.OrdinalIgnoreCase);

    private static string DescribeLayer(ContentLayer layer) => layer.Kind switch
    {
        ContentLayerKind.BaseGame => "base-game",
        ContentLayerKind.Mod => "mod",
        _ => "content-layer",
    };

    private static bool IsFileSystemException(Exception exception) => exception is
        IOException or
        UnauthorizedAccessException or
        SecurityException;

    private sealed record DocumentCandidate(
        ContentLayer Layer,
        string PhysicalPath,
        VirtualPath VirtualPath,
        SourceDocumentKind Kind,
        ContentCategory Category);
}
