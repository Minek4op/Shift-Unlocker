using Microsoft.Win32;

namespace ShiftUnlocker.Core.Context;

public interface ITargetContext : IDisposable
{
    bool IsOnline { get; }
    bool IsWinPE { get; }
    string TargetRootPath { get; }
    string WindowsDirectory { get; }
    string System32Directory { get; }
    string CurrentControlSetPath { get; }

    IReadOnlyList<UserProfileInfo> UserProfiles { get; }

    string MapPath(string pathWithEnvOrDrive);

    RegistryKey? OpenKey(string rootHive, string subKey, bool writable = false);
    void DeleteSubKeyTree(string rootHive, string subKey);
    void DeleteValue(string rootHive, string subKey, string valueName);
    void SetValue(string rootHive, string subKey, string valueName, object value, RegistryValueKind valueKind);
    object? GetValue(string rootHive, string subKey, string valueName, object? defaultValue = null);
    string[] GetSubKeyNames(string rootHive, string subKey);
    string[] GetValueNames(string rootHive, string subKey);

    void Flush();
}
