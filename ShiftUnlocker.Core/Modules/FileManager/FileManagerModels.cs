namespace ShiftUnlocker.Core.Modules.FileManager;

public record FileSystemItem(
    string Name,
    string FullPath,
    bool IsDirectory,
    long SizeBytes,
    DateTime LastWriteTime,
    FileAttributes Attributes,
    string AttributesString,
    bool IsHiddenOrSystem);

public record PeHeaderInfo(
    bool IsValidPe,
    string Architecture,
    DateTime? TimeDateStamp,
    string Subsystem,
    int NumberOfSections);
