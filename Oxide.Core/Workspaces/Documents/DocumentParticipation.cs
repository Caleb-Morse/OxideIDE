using Oxide.Core.Workspaces.Configuration;

namespace Oxide.Core.Workspaces.Documents;

public sealed record DocumentParticipation(
    DocumentParticipationKind Kind,
    ContentCategory Category,
    string Explanation,
    ContentLayerId? CausedByLayerId = null,
    DocumentId? ShadowingDocumentId = null,
    ContentLayerReplacementRule? ReplacementRule = null)
{
    public bool Participates => Kind is DocumentParticipationKind.Participating;

    public static DocumentParticipation Participating(ContentCategory category) =>
        new(DocumentParticipationKind.Participating, category, "The document participates in semantic construction.");
}
