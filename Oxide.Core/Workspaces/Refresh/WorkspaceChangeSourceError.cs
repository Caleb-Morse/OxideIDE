using Oxide.Core.Workspaces.Configuration;

namespace Oxide.Core.Workspaces.Refresh;

public sealed record WorkspaceChangeSourceError(
    string Message,
    ContentLayerId? LayerId = null,
    string? PhysicalPath = null,
    Exception? Exception = null,
    bool RequiresFullRescan = true);
