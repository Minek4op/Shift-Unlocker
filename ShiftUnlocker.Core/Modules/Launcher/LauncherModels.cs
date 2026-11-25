namespace ShiftUnlocker.Core.Modules.Launcher;

public record UtilityItem(
    string Id,
    string Name,
    string Category,
    string Description,
    string ExecutablePath,
    string Arguments,
    bool RunAsAdmin,
    bool RunRandomName,
    bool ExistsOnDisk,
    bool IsBuiltIn,
    bool IsUserAdded = false,
    string? EmbeddedResourceName = null);
