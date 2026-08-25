namespace Oxide.Core.Workspaces.Documents;

public enum DocumentParticipationKind
{
    Participating,
    ShadowedByHigherLayerPath,
    ExcludedByReplacementPath,
}
