namespace Oxide.Core.Workspaces.Navigation;

public enum SourceNavigationStatus
{
    Resolved,
    SnapshotVersionMismatch,
    DocumentNotFound,
    SourceIdentityMismatch,
    DocumentFailed,
    TextUnavailable,
    UnsupportedDocument,
    InvalidSpan,
}
