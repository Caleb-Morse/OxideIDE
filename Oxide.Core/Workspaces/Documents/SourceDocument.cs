using System.Collections.Immutable;
using Oxide.Core.Workspaces.Configuration;
using Oxide.Core.Workspaces.Loading;
using Oxide.Syntax.Localisation;
using Oxide.Syntax.Parsing;
using Oxide.Syntax.Text;

namespace Oxide.Core.Workspaces.Documents;

public sealed record SourceDocument(
    DocumentId Id,
    ContentLayer Layer,
    string PhysicalPath,
    VirtualPath VirtualPath,
    SourceDocumentKind Kind,
    DocumentLoadStatus LoadStatus,
    DocumentParticipation Participation,
    SourceText? Text,
    SyntaxTree? SyntaxTree,
    LocalisationSyntaxTree? LocalisationSyntaxTree,
    ImmutableArray<WorkspaceDiagnostic> Diagnostics)
{
    public bool IsLoaded => LoadStatus is DocumentLoadStatus.Loaded;

    public bool Participates => Participation.Participates;

    public SourceIdentity SourceIdentity => new(Id, Layer.Id, VirtualPath, PhysicalPath);
}
