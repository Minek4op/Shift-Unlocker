namespace ShiftUnlocker.Core.Modules.Autorun;

public record AutorunEntry(
    string Id,
    string Name,
    string Command,
    string ResolvedFilePath,
    bool FileExists,
    string LocationType,
    string Hive,
    string KeyOrDirectoryPath,
    string ValueName,
    bool IsEnabled,
    bool IsSuspicious,
    string SuspiciousReason,
    string? Sha256 = null,
    DateTime? LastModified = null);
