using Microsoft.Win32;

namespace ShiftUnlocker.Core.Modules.RegEditor;

public record RegistryValueDisplay(
    string Name,
    RegistryValueKind Kind,
    string KindName,
    object Value,
    string FormattedData);

public record RegistryBookmark(
    string Name,
    string Hive,
    string SubKey,
    string Description);
