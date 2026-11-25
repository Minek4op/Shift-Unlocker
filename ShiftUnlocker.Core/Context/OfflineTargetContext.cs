using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using ShiftUnlocker.Native;

namespace ShiftUnlocker.Core.Context;

public class OfflineTargetContext : ITargetContext
{
    public bool IsOnline => false;
    public bool IsWinPE { get; }
    public string TargetRootPath { get; }
    public string WindowsDirectory { get; }
    public string System32Directory { get; }
    public string CurrentControlSetPath { get; private set; } = "ControlSet001";

    private readonly string _guid;
    private readonly Dictionary<string, string> _loadedHives = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<UserProfileInfo> _userProfiles = new();
    private bool _disposed;

    public IReadOnlyList<UserProfileInfo> UserProfiles => _userProfiles;

    public OfflineTargetContext(string targetWindowsDir)
    {
        IsWinPE = WindowsInstallationDetector.IsRunningInWinPE();
        WindowsDirectory = targetWindowsDir;
        TargetRootPath = Path.GetPathRoot(targetWindowsDir) ?? @"C:\";
        System32Directory = Path.Combine(WindowsDirectory, "System32");
        _guid = Guid.NewGuid().ToString("N")[..8];

        LoadHives();
        DetectCurrentControlSet();
        LoadUserProfiles();
    }

    private void LoadHives()
    {
        string configDir = Path.Combine(System32Directory, "config");

        TryLoadHive(Advapi32.HKEY_LOCAL_MACHINE, $"SU_SYSTEM_{_guid}", Path.Combine(configDir, "SYSTEM"), "SYSTEM");
        TryLoadHive(Advapi32.HKEY_LOCAL_MACHINE, $"SU_SOFTWARE_{_guid}", Path.Combine(configDir, "SOFTWARE"), "SOFTWARE");
        TryLoadHive(Advapi32.HKEY_LOCAL_MACHINE, $"SU_SAM_{_guid}", Path.Combine(configDir, "SAM"), "SAM");
        TryLoadHive(Advapi32.HKEY_LOCAL_MACHINE, $"SU_SECURITY_{_guid}", Path.Combine(configDir, "SECURITY"), "SECURITY");
        TryLoadHive(Advapi32.HKEY_LOCAL_MACHINE, $"SU_DEFAULT_{_guid}", Path.Combine(configDir, "DEFAULT"), "DEFAULT");
    }

    private bool TryLoadHive(IntPtr rootHandle, string keyName, string hiveFilePath, string alias)
    {
        if (!File.Exists(hiveFilePath)) return false;

        int ret = Advapi32.RegLoadKeyW(rootHandle, keyName, hiveFilePath);
        if (ret == Win32Error.ERROR_SUCCESS || ret == Win32Error.ERROR_ALREADY_EXISTS)
        {
            _loadedHives[alias] = keyName;
            return true;
        }

        return false;
    }

    private void DetectCurrentControlSet()
    {
        if (_loadedHives.TryGetValue("SYSTEM", out var systemKeyName))
        {
            try
            {
                using var sysKey = Registry.LocalMachine.OpenSubKey(systemKeyName);
                using var selKey = sysKey?.OpenSubKey("Select");
                if (selKey != null)
                {
                    int current = Convert.ToInt32(selKey.GetValue("Current", 1));
                    CurrentControlSetPath = $"ControlSet{current:D3}";
                }
            }
            catch
            {
                CurrentControlSetPath = "ControlSet001";
            }
        }
    }

    private void LoadUserProfiles()
    {
        string usersDir = Path.Combine(TargetRootPath, "Users");
        if (!Directory.Exists(usersDir)) return;

        try
        {
            foreach (var userDir in Directory.GetDirectories(usersDir))
            {
                string userName = Path.GetFileName(userDir);
                if (userName.Equals("Public", StringComparison.OrdinalIgnoreCase) ||
                    userName.Equals("Default User", StringComparison.OrdinalIgnoreCase) ||
                    userName.Equals("All Users", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string ntuser = Path.Combine(userDir, "NTUSER.DAT");
                if (File.Exists(ntuser))
                {
                    string safeUser = new string(userName.Where(char.IsLetterOrDigit).ToArray());
                    string keyName = $"SU_USER_{safeUser}_{_guid}";
                    string? loadedKey = null;

                    if (TryLoadHive(Advapi32.HKEY_USERS, keyName, ntuser, $"USER_{userName}"))
                    {
                        loadedKey = keyName;
                    }

                    _userProfiles.Add(new UserProfileInfo(userName, userDir, ntuser, loadedKey));
                }
            }
        }
        catch
        {
            // пропускаем закрытые папки
        }
    }

    public string MapPath(string pathWithEnvOrDrive)
    {
        if (string.IsNullOrWhiteSpace(pathWithEnvOrDrive)) return pathWithEnvOrDrive;

        string mapped = pathWithEnvOrDrive;
        mapped = mapped.Replace("%SystemRoot%", WindowsDirectory, StringComparison.OrdinalIgnoreCase);
        mapped = mapped.Replace("%windir%", WindowsDirectory, StringComparison.OrdinalIgnoreCase);
        mapped = mapped.Replace("%SystemDrive%", TargetRootPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
        mapped = mapped.Replace("%ProgramFiles%", Path.Combine(TargetRootPath, "Program Files"), StringComparison.OrdinalIgnoreCase);
        mapped = mapped.Replace("%ProgramFiles(x86)%", Path.Combine(TargetRootPath, "Program Files (x86)"), StringComparison.OrdinalIgnoreCase);
        mapped = mapped.Replace("%ProgramData%", Path.Combine(TargetRootPath, "ProgramData"), StringComparison.OrdinalIgnoreCase);

        if (mapped.Length > 2 && mapped[1] == ':' && !mapped.StartsWith(TargetRootPath, StringComparison.OrdinalIgnoreCase))
        {
            string onTarget = Path.Combine(TargetRootPath, mapped[3..]);
            if (File.Exists(onTarget) || Directory.Exists(onTarget))
            {
                return onTarget;
            }
        }

        return mapped;
    }

    private (RegistryKey rootKey, string mappedSubKey) ResolvePath(string rootHive, string subKey)
    {
        string normHive = rootHive.ToUpperInvariant();
        string cleanSubKey = subKey.TrimStart('\\');

        if (normHive is "HKLM" or "HKEY_LOCAL_MACHINE")
        {
            if (cleanSubKey.StartsWith("SYSTEM", StringComparison.OrdinalIgnoreCase))
            {
                string remainder = cleanSubKey.Length > 6 ? cleanSubKey[7..].TrimStart('\\') : "";
                if (remainder.StartsWith("CurrentControlSet", StringComparison.OrdinalIgnoreCase))
                {
                    remainder = remainder.Length > 17 ? remainder[18..].TrimStart('\\') : "";
                    remainder = string.IsNullOrEmpty(remainder) ? CurrentControlSetPath : Path.Combine(CurrentControlSetPath, remainder);
                }

                if (_loadedHives.TryGetValue("SYSTEM", out var sysKey))
                {
                    return (Registry.LocalMachine, string.IsNullOrEmpty(remainder) ? sysKey : Path.Combine(sysKey, remainder));
                }
            }
            else if (cleanSubKey.StartsWith("SOFTWARE", StringComparison.OrdinalIgnoreCase))
            {
                string remainder = cleanSubKey.Length > 8 ? cleanSubKey[9..].TrimStart('\\') : "";
                if (_loadedHives.TryGetValue("SOFTWARE", out var softKey))
                {
                    return (Registry.LocalMachine, string.IsNullOrEmpty(remainder) ? softKey : Path.Combine(softKey, remainder));
                }
            }
            else if (cleanSubKey.StartsWith("SAM", StringComparison.OrdinalIgnoreCase))
            {
                string remainder = cleanSubKey.Length > 3 ? cleanSubKey[4..].TrimStart('\\') : "";
                if (_loadedHives.TryGetValue("SAM", out var samKey))
                {
                    return (Registry.LocalMachine, string.IsNullOrEmpty(remainder) ? samKey : Path.Combine(samKey, remainder));
                }
            }
            else if (cleanSubKey.StartsWith("SECURITY", StringComparison.OrdinalIgnoreCase))
            {
                string remainder = cleanSubKey.Length > 8 ? cleanSubKey[9..].TrimStart('\\') : "";
                if (_loadedHives.TryGetValue("SECURITY", out var secKey))
                {
                    return (Registry.LocalMachine, string.IsNullOrEmpty(remainder) ? secKey : Path.Combine(secKey, remainder));
                }
            }

            return (Registry.LocalMachine, cleanSubKey);
        }

        if (normHive is "HKCU" or "HKEY_CURRENT_USER")
        {
            var firstUser = _userProfiles.FirstOrDefault(u => !string.IsNullOrEmpty(u.LoadedHiveKey));
            if (firstUser?.LoadedHiveKey != null)
            {
                return (Registry.Users, Path.Combine(firstUser.LoadedHiveKey, cleanSubKey));
            }

            if (_loadedHives.TryGetValue("DEFAULT", out var defKey))
            {
                return (Registry.LocalMachine, Path.Combine(defKey, cleanSubKey));
            }

            return (Registry.CurrentUser, cleanSubKey);
        }

        if (normHive is "HKCR" or "HKEY_CLASSES_ROOT")
        {
            if (_loadedHives.TryGetValue("SOFTWARE", out var softKey))
            {
                return (Registry.LocalMachine, Path.Combine(softKey, "Classes", cleanSubKey));
            }
            return (Registry.ClassesRoot, cleanSubKey);
        }

        if (normHive is "HKU" or "HKEY_USERS")
        {
            return (Registry.Users, cleanSubKey);
        }

        return (Registry.LocalMachine, cleanSubKey);
    }

    public RegistryKey? OpenKey(string rootHive, string subKey, bool writable = false)
    {
        var (root, mappedPath) = ResolvePath(rootHive, subKey);
        return root.OpenSubKey(mappedPath, writable);
    }

    public void DeleteSubKeyTree(string rootHive, string subKey)
    {
        var (root, mappedPath) = ResolvePath(rootHive, subKey);
        root.DeleteSubKeyTree(mappedPath, false);
    }

    public void DeleteValue(string rootHive, string subKey, string valueName)
    {
        using var key = OpenKey(rootHive, subKey, true);
        key?.DeleteValue(valueName, false);
    }

    public void SetValue(string rootHive, string subKey, string valueName, object value, RegistryValueKind valueKind)
    {
        var (root, mappedPath) = ResolvePath(rootHive, subKey);
        using var key = root.CreateSubKey(mappedPath, true);
        key.SetValue(valueName, value, valueKind);
    }

    public object? GetValue(string rootHive, string subKey, string valueName, object? defaultValue = null)
    {
        using var key = OpenKey(rootHive, subKey, false);
        return key?.GetValue(valueName, defaultValue);
    }

    public string[] GetSubKeyNames(string rootHive, string subKey)
    {
        using var key = OpenKey(rootHive, subKey, false);
        return key?.GetSubKeyNames() ?? Array.Empty<string>();
    }

    public string[] GetValueNames(string rootHive, string subKey)
    {
        using var key = OpenKey(rootHive, subKey, false);
        return key?.GetValueNames() ?? Array.Empty<string>();
    }

    public void Flush()
    {
        Registry.LocalMachine.Flush();
        Registry.Users.Flush();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Flush();
        GC.Collect();
        GC.WaitForPendingFinalizers();

        foreach (var profile in _userProfiles)
        {
            if (!string.IsNullOrEmpty(profile.LoadedHiveKey))
            {
                UnloadWithRetry(Advapi32.HKEY_USERS, profile.LoadedHiveKey);
            }
        }

        foreach (var kvp in _loadedHives)
        {
            UnloadWithRetry(Advapi32.HKEY_LOCAL_MACHINE, kvp.Value);
        }
    }

    private void UnloadWithRetry(IntPtr rootHandle, string keyName, int maxRetries = 5)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            int ret = Advapi32.RegUnLoadKeyW(rootHandle, keyName);
            if (ret == Win32Error.ERROR_SUCCESS) return;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            Thread.Sleep(100 * (i + 1));
        }
    }
}
