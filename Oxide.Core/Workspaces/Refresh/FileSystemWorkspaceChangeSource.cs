using System.Collections.Immutable;
using System.Threading.Channels;
using Oxide.Core.Workspaces.Configuration;

namespace Oxide.Core.Workspaces.Refresh;

public sealed class FileSystemWorkspaceChangeSource : IWorkspaceChangeSource
{
    private readonly WorkspaceConfiguration _configuration;
    private readonly WorkspaceChangeSourceOptions _options;
    private readonly object _lifecycleGate = new();
    private WatchSession? _session;
    private long _generation;
    private bool _disposed;

    public FileSystemWorkspaceChangeSource(
        WorkspaceConfiguration configuration,
        WorkspaceChangeSourceOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _configuration = configuration;
        _options = options ?? new WorkspaceChangeSourceOptions();
    }

    public event Action<WorkspaceChangeBatch>? ChangesAvailable;

    public event Action<WorkspaceChangeSourceError>? Error;

    public bool IsRunning
    {
        get
        {
            lock (_lifecycleGate)
            {
                return _session is not null;
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lifecycleGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_session is not null)
            {
                return Task.CompletedTask;
            }

            var generation = ++_generation;
            var cancellation = new CancellationTokenSource();
            var channel = Channel.CreateBounded<RawChange>(new BoundedChannelOptions(_options.QueueCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait,
                AllowSynchronousContinuations = false,
            });
            var watchers = CreateWatchers(generation, channel.Writer);
            var session = new WatchSession(generation, cancellation, channel, watchers);
            _session = session;
            session.Processor = ProcessAsync(session);
            foreach (var layer in _configuration.Layers.Where(layer => layer.IsEnabled && !Directory.Exists(layer.RootPath)))
            {
                Queue(channel.Writer, new RawChange(
                    generation,
                    layer,
                    WorkspaceChangeKind.Uncertain,
                    null,
                    null,
                    new DirectoryNotFoundException($"Content root does not exist: {layer.RootPath}")));
            }

            foreach (var watcher in watchers)
            {
                watcher.EnableRaisingEvents = true;
            }
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        WatchSession? session;
        lock (_lifecycleGate)
        {
            session = _session;
            _session = null;
            _generation++;
        }

        if (session is null)
        {
            return;
        }

        foreach (var watcher in session.Watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }

        session.Cancellation.Cancel();
        session.Channel.Writer.TryComplete();
        try
        {
            await session.Processor.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (session.Cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            session.Cancellation.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (_lifecycleGate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        await StopAsync().ConfigureAwait(false);
    }

    private ImmutableArray<FileSystemWatcher> CreateWatchers(long generation, ChannelWriter<RawChange> writer)
    {
        var watchers = ImmutableArray.CreateBuilder<FileSystemWatcher>();
        try
        {
            foreach (var layer in _configuration.Layers.Where(layer => layer.IsEnabled && Directory.Exists(layer.RootPath)))
            {
                var watcher = new FileSystemWatcher(layer.RootPath)
                {
                    IncludeSubdirectories = true,
                    Filter = "*",
                    NotifyFilter = NotifyFilters.FileName
                        | NotifyFilters.DirectoryName
                        | NotifyFilters.LastWrite
                        | NotifyFilters.CreationTime
                        | NotifyFilters.Size,
                };
                watcher.Created += (_, args) => Queue(writer, new RawChange(
                    generation, layer, WorkspaceChangeKind.Created, null, args.FullPath, null));
                watcher.Changed += (_, args) => Queue(writer, new RawChange(
                    generation, layer, WorkspaceChangeKind.Changed, args.FullPath, args.FullPath, null));
                watcher.Deleted += (_, args) => Queue(writer, new RawChange(
                    generation, layer, WorkspaceChangeKind.Deleted, args.FullPath, null, null));
                watcher.Renamed += (_, args) => Queue(writer, new RawChange(
                    generation, layer, WorkspaceChangeKind.Renamed, args.OldFullPath, args.FullPath, null));
                watcher.Error += (_, args) => Queue(writer, new RawChange(
                    generation,
                    layer,
                    WorkspaceChangeKind.Uncertain,
                    null,
                    null,
                    args.GetException()));
                watchers.Add(watcher);
            }

            return watchers.ToImmutable();
        }
        catch
        {
            foreach (var watcher in watchers)
            {
                watcher.Dispose();
            }

            throw;
        }
    }

    private void Queue(ChannelWriter<RawChange> writer, RawChange change)
    {
        if (change.Generation != Volatile.Read(ref _generation))
        {
            return;
        }

        if (!writer.TryWrite(change))
        {
            WatchSession? session;
            lock (_lifecycleGate)
            {
                session = _session;
            }

            if (session is not null && session.Generation == change.Generation)
            {
                Interlocked.Exchange(ref session.Overflowed, 1);
                Interlocked.Increment(ref session.DroppedEventCount);
            }
        }
    }

    private async Task ProcessAsync(WatchSession session)
    {
        var reader = session.Channel.Reader;
        var cancellationToken = session.Cancellation.Token;
        while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var rawChanges = new List<RawChange>();
            while (reader.TryRead(out var initial))
            {
                rawChanges.Add(initial);
            }

            if (_options.DebounceInterval > TimeSpan.Zero)
            {
                await Task.Delay(_options.DebounceInterval, cancellationToken).ConfigureAwait(false);
            }

            while (reader.TryRead(out var pending))
            {
                rawChanges.Add(pending);
            }

            try
            {
                Publish(session, rawChanges);
            }
            catch (Exception exception)
            {
                RaiseError(new WorkspaceChangeSourceError(
                    $"Could not process filesystem changes: {exception.Message}",
                    Exception: exception));
                RaiseChanges(new WorkspaceChangeBatch(
                    [],
                    requiresFullRescan: true,
                    fullRescanReason: "Filesystem changes could not be classified safely.",
                    rawEventCount: rawChanges.Count));
            }
        }
    }

    private void Publish(WatchSession session, List<RawChange> rawChanges)
    {
        if (session.Generation != Volatile.Read(ref _generation))
        {
            return;
        }

        var documentChanges = new List<DocumentChange>();
        var droppedEventCount = Interlocked.Exchange(ref session.DroppedEventCount, 0);
        var requiresFullRescan = Interlocked.Exchange(ref session.Overflowed, 0) != 0;
        string? fullRescanReason = requiresFullRescan
            ? "The filesystem event queue overflowed."
            : null;
        foreach (var raw in rawChanges)
        {
            if (raw.Error is not null)
            {
                requiresFullRescan = true;
                fullRescanReason ??= "The filesystem watcher reported an uncertain state.";
                RaiseError(new WorkspaceChangeSourceError(
                    raw.Error.Message,
                    raw.Layer.Id,
                    raw.Layer.RootPath,
                    raw.Error));
                continue;
            }

            if (IsDescriptorChange(raw))
            {
                requiresFullRescan = true;
                fullRescanReason ??= "A mod descriptor changed and may alter content participation.";
                continue;
            }

            AddDocumentChanges(raw, documentChanges);
        }

        var batch = WorkspaceChangeCoalescer.Coalesce(
            documentChanges,
            rawChanges.Count + droppedEventCount,
            requiresFullRescan,
            fullRescanReason);
        if (batch.Changes.IsEmpty && !batch.RequiresFullRescan)
        {
            return;
        }

        RaiseChanges(batch);
    }

    private static void AddDocumentChanges(RawChange raw, List<DocumentChange> changes)
    {
        var previous = raw.PreviousPhysicalPath is null
            ? null
            : WorkspaceChangeClassifier.Classify(raw.Layer, raw.PreviousPhysicalPath);
        var current = raw.CurrentPhysicalPath is null
            ? null
            : WorkspaceChangeClassifier.Classify(raw.Layer, raw.CurrentPhysicalPath);
        var previousSupported = previous?.IsSupported is true;
        var currentSupported = current?.IsSupported is true;
        if (!previousSupported && !currentSupported)
        {
            return;
        }

        var kind = raw.Kind;
        if (kind is WorkspaceChangeKind.Renamed && previousSupported && !currentSupported)
        {
            kind = WorkspaceChangeKind.Deleted;
        }
        else if (kind is WorkspaceChangeKind.Renamed && !previousSupported && currentSupported)
        {
            kind = WorkspaceChangeKind.Created;
        }

        if (kind is WorkspaceChangeKind.Renamed
            && previous!.Category != current!.Category)
        {
            changes.Add(CreateDocumentChange(
                WorkspaceChangeKind.Deleted,
                previous,
                null,
                raw));
            changes.Add(CreateDocumentChange(
                WorkspaceChangeKind.Created,
                null,
                current,
                raw));
            return;
        }

        var classification = currentSupported ? current! : previous!;
        changes.Add(CreateDocumentChange(kind, previous, current, raw, classification));
    }

    private static DocumentChange CreateDocumentChange(
        WorkspaceChangeKind kind,
        WorkspaceChangePathResult? previous,
        WorkspaceChangePathResult? current,
        RawChange raw,
        WorkspaceChangePathResult? classification = null)
    {
        classification ??= current?.IsSupported is true ? current : previous;
        var previousSource = kind is WorkspaceChangeKind.Created ? null : previous?.Source;
        var currentSource = kind is WorkspaceChangeKind.Deleted ? null : current?.Source;
        return new DocumentChange(
            new WorkspaceChange(
            kind,
            previousSource,
            currentSource,
            DateTimeOffset.UtcNow,
            WorkspaceChangeOrigin.Watcher),
            classification!.DocumentKind!.Value,
            classification.Category!.Value);
    }

    private static bool IsDescriptorChange(RawChange raw) =>
        raw.Layer.Kind is ContentLayerKind.Mod
        && (IsDescriptorPath(raw.Layer, raw.PreviousPhysicalPath)
            || IsDescriptorPath(raw.Layer, raw.CurrentPhysicalPath));

    private static bool IsDescriptorPath(ContentLayer layer, string? path) =>
        path is not null
        && string.Equals(
            Path.GetFullPath(path),
            Path.Combine(layer.RootPath, "descriptor.mod"),
            StringComparison.OrdinalIgnoreCase);

    private void RaiseChanges(WorkspaceChangeBatch batch)
    {
        foreach (var handler in ChangesAvailable?.GetInvocationList().Cast<Action<WorkspaceChangeBatch>>() ?? [])
        {
            try
            {
                handler(batch);
            }
            catch (Exception exception)
            {
                RaiseError(new WorkspaceChangeSourceError(
                    $"A workspace change subscriber failed: {exception.Message}",
                    Exception: exception,
                    RequiresFullRescan: false));
            }
        }
    }

    private void RaiseError(WorkspaceChangeSourceError error)
    {
        foreach (var handler in Error?.GetInvocationList().Cast<Action<WorkspaceChangeSourceError>>() ?? [])
        {
            try
            {
                handler(error);
            }
            catch
            {
                // Error observers cannot be allowed to terminate the watcher worker.
            }
        }
    }

    private sealed record RawChange(
        long Generation,
        ContentLayer Layer,
        WorkspaceChangeKind Kind,
        string? PreviousPhysicalPath,
        string? CurrentPhysicalPath,
        Exception? Error);

    private sealed class WatchSession(
        long generation,
        CancellationTokenSource cancellation,
        Channel<RawChange> channel,
        ImmutableArray<FileSystemWatcher> watchers)
    {
        public long Generation { get; } = generation;

        public CancellationTokenSource Cancellation { get; } = cancellation;

        public Channel<RawChange> Channel { get; } = channel;

        public ImmutableArray<FileSystemWatcher> Watchers { get; } = watchers;

        public Task Processor { get; set; } = Task.CompletedTask;

        public int Overflowed;

        public int DroppedEventCount;
    }
}
