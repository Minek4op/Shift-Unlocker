using Microsoft.Win32;

namespace ShiftUnlocker.Core.Context;

public class OnlineTargetContext : ITargetContext
{
    public bool IsOnline => true;
    public bool IsWinPE { get; }
    public string TargetRootPath { get; }
    public string WindowsDirectory { get; }
    public string System32Directory { get; }
    public string CurrentControlSetPath => @"SYSTEM\CurrentControlSet";

    public IReadOnlyList<UserProfileInfo> UserProfiles { get; private set; }

    public OnlineTargetContext()
    {
        IsWinPE = WindowsInstallationDetector.IsRunningInWinPE();
        WindowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        TargetRootPath = Path.GetPathRoot(WindowsDirectory) ?? @"C:\";
        System32Directory = Path.Combine(WindowsDirectory, "System32");

        UserProfiles = LoadUserProfiles();
    }

    private List<UserProfileInfo> LoadUserProfiles()
    {
        var list = new List<UserProfileInfo>();
        try
        {
            using var plKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList");
            if (plKey != null)
            {
                foreach (string subKeyName in plKey.GetSubKeyNames())
                {
                    using var prof = plKey.OpenSubKey(subKeyName);
                    if (prof != null)
                    {
                        string? path = prof.GetValue("ProfileImagePath")?.ToString();
                        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                        {
                            string userName = Path.GetFileName(path);
                            string ntUserDat = Path.Combine(path, "NTUSER.DAT");
                            list.Add(new UserProfileInfo(userName, path, ntUserDat, null));
                        }
                    }
                }
            }
        }
        catch
        {
            // глушим ошибку
        }

        return list;
    }

    public string MapPath(string pathWithEnvOrDrive)
    {
        return Environment.ExpandEnvironmentVariables(pathWithEnvOrDrive);
    }

    private RegistryKey GetRootKey(string rootHive)
    {
        return rootHive.ToUpperInvariant() switch
        {
            "HKLM" or "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
            "HKCU" or "HKEY_CURRENT_USER" => Registry.CurrentUser,
            "HKU" or "HKEY_USERS" => Registry.Users,
            "HKCR" or "HKEY_CLASSES_ROOT" => Registry.ClassesRoot,
            "HKCC" or "HKEY_CURRENT_CONFIG" => Registry.CurrentConfig,
            _ => Registry.LocalMachine
        };
    }

    public RegistryKey? OpenKey(string rootHive, string subKey, bool writable = false)
    {
        var root = GetRootKey(rootHive);
        return root.OpenSubKey(subKey, writable);
    }

    public void DeleteSubKeyTree(string rootHive, string subKey)
    {
        var root = GetRootKey(rootHive);
        root.DeleteSubKeyTree(subKey, false);
    }

    public void DeleteValue(string rootHive, string subKey, string valueName)
    {
        using var key = OpenKey(rootHive, subKey, true);
        key?.DeleteValue(valueName, false);
    }

    public void SetValue(string rootHive, string subKey, string valueName, object value, RegistryValueKind valueKind)
    {
        var root = GetRootKey(rootHive);
        using var key = root.CreateSubKey(subKey, true);
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
        Registry.CurrentUser.Flush();
    }

    public void Dispose()
    {
        Flush();
    }
}
