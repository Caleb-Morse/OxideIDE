using System.Collections.Immutable;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Documents;
using Oxide.Core.Workspaces.Loading;
using Oxide.Core.Semantics.Snapshots;

namespace Oxide.Core.Workspaces.Snapshots;

public sealed class WorkspaceSnapshot
{
    internal WorkspaceSnapshot(
        long version,
        WorkspaceConfiguration configuration,
        ImmutableArray<ContentLayer> layers,
        ImmutableArray<SourceDocument> documents,
        ImmutableArray<WorkspaceDiagnostic> diagnostics,
        SemanticSnapshot semantics)
    {
        Version = version;
        Configuration = configuration;
        Layers = layers;
        Documents = documents;
        Diagnostics = diagnostics;
        Semantics = semantics;
        LoadedAt = DateTimeOffset.UtcNow;
        DocumentsById = documents.ToImmutableDictionary(document => document.Id);
        DocumentsByVirtualPath = documents
            .GroupBy(document => document.VirtualPath)
            .ToImmutableDictionary(
                group => group.Key,
                group => group.OrderBy(document => document.Layer.Position).ToImmutableArray());
    }

    public long Version { get; }

    public WorkspaceConfiguration Configuration { get; }

    public DateTimeOffset LoadedAt { get; }

    public ImmutableArray<ContentLayer> Layers { get; }

    public ImmutableArray<SourceDocument> Documents { get; }

    public ImmutableDictionary<DocumentId, SourceDocument> DocumentsById { get; }

    public ImmutableDictionary<VirtualPath, ImmutableArray<SourceDocument>> DocumentsByVirtualPath { get; }

    public ImmutableArray<WorkspaceDiagnostic> Diagnostics { get; }

    public SemanticSnapshot Semantics { get; }
}
