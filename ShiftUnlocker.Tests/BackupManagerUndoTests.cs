using Microsoft.Win32;
using ShiftUnlocker.Core.Context;
using ShiftUnlocker.Core.Safety;
using Xunit;

namespace ShiftUnlocker.Tests;

public class BackupManagerUndoTests
{
    private sealed class InMemoryRegistryContext : ITargetContext
    {
        private readonly Dictionary<string, Dictionary<string, (object Value, RegistryValueKind Kind)>> _store =
            new(StringComparer.OrdinalIgnoreCase);

        public bool IsOnline => false;
        public bool IsWinPE => false;
        public string TargetRootPath => @"C:\";
        public string WindowsDirectory => @"C:\Windows";
        public string System32Directory => @"C:\Windows\System32";
        public string CurrentControlSetPath => "ControlSet001";
        public IReadOnlyList<UserProfileInfo> UserProfiles => [];

        private static string Key(string hive, string subKey) => $@"{hive}\{subKey}".TrimEnd('\\');

        public void Seed(string hive, string subKey, string name, object value, RegistryValueKind kind)
        {
            string k = Key(hive, subKey);
            if (!_store.TryGetValue(k, out var dict))
            {
                dict = new Dictionary<string, (object, RegistryValueKind)>(StringComparer.OrdinalIgnoreCase);
                _store[k] = dict;
            }
            dict[name] = (value, kind);
        }

        public RegistryValueKind? GetKind(string hive, string subKey, string name)
        {
            string k = Key(hive, subKey);
            return _store.TryGetValue(k, out var dict) && dict.TryGetValue(name, out var entry) ? entry.Kind : null;
        }

        public string MapPath(string pathWithEnvOrDrive) => pathWithEnvOrDrive;
        public RegistryKey? OpenKey(string rootHive, string subKey, bool writable = false) => null;

        public void DeleteSubKeyTree(string rootHive, string subKey)
        {
            string prefix = Key(rootHive, subKey);
            foreach (var k in _store.Keys.Where(x => x.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                                                      x.StartsWith(prefix + @"\", StringComparison.OrdinalIgnoreCase)).ToList())
            {
                _store.Remove(k);
            }
        }

        public void DeleteValue(string rootHive, string subKey, string valueName)
        {
            string k = Key(rootHive, subKey);
            if (_store.TryGetValue(k, out var dict)) dict.Remove(valueName);
        }

        public object? GetValue(string rootHive, string subKey, string valueName)
        {
            string k = Key(rootHive, subKey);
            return _store.TryGetValue(k, out var dict) && dict.TryGetValue(valueName, out var entry) ? entry.Value : null;
        }

        public object? GetValue(string rootHive, string subKey, string valueName, object? defaultValue = null)
            => GetValue(rootHive, subKey, valueName) ?? defaultValue;

        public void Flush() { }

        public void SetValue(string rootHive, string subKey, string valueName, object value, RegistryValueKind kind = RegistryValueKind.String)
            => Seed(rootHive, subKey, valueName, value, kind);

        public string[] GetSubKeyNames(string rootHive, string subKey)
        {
            string prefix = Key(rootHive, subKey) + @"\";
            return _store.Keys
                .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(k => k[prefix.Length..].Split('\\')[0])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public string[] GetValueNames(string rootHive, string subKey)
        {
            string k = Key(rootHive, subKey);
            return _store.TryGetValue(k, out var dict) ? dict.Keys.ToArray() : [];
        }

        public void Dispose() { }
    }

    [Fact]
    public void BackupAndSetRegistryValue_UndoPreservesDWordValue()
    {
        var ctx = new InMemoryRegistryContext();
        ctx.Seed("HKLM", @"Software\Test", "Flag", 1, RegistryValueKind.DWord);
        var bm = new BackupManager();

        var res = bm.BackupAndSetRegistryValue(ctx, "HKLM", @"Software\Test", "Flag", 0, RegistryValueKind.DWord, "Disable flag");
        Assert.True(res.Success);
        Assert.Equal(0, ctx.GetValue("HKLM", @"Software\Test", "Flag"));

        var undo = bm.UndoLast();
        Assert.True(undo.Success);
        Assert.Equal(1, ctx.GetValue("HKLM", @"Software\Test", "Flag"));
        Assert.Equal(RegistryValueKind.DWord, ctx.GetKind("HKLM", @"Software\Test", "Flag"));
    }

    [Fact]
    public void BackupAndDeleteRegistryValue_UndoPreservesInferredDWordKind()
    {
        var ctx = new InMemoryRegistryContext();
        ctx.Seed("HKCU", @"Software\Test", "Timeout", 42, RegistryValueKind.DWord);
        var bm = new BackupManager();

        var res = bm.BackupAndDeleteRegistryValue(ctx, "HKCU", @"Software\Test", "Timeout", "Delete timeout");
        Assert.True(res.Success);
        Assert.Null(ctx.GetValue("HKCU", @"Software\Test", "Timeout"));

        var undo = bm.UndoLast();
        Assert.True(undo.Success);
        Assert.Equal(42, ctx.GetValue("HKCU", @"Software\Test", "Timeout"));
        Assert.Equal(RegistryValueKind.DWord, ctx.GetKind("HKCU", @"Software\Test", "Timeout"));
    }

    [Fact]
    public void EnumerateUserProfiles_FindsProfilesWithNtUserDatAndSkipsSystemDirs()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "SU_ProfileTest_" + Guid.NewGuid().ToString("N"));
        string winDir = Path.Combine(tempRoot, "Windows");
        try
        {
            Directory.CreateDirectory(winDir);
            Directory.CreateDirectory(Path.Combine(tempRoot, "Users", "Public"));
            Directory.CreateDirectory(Path.Combine(tempRoot, "Users", "Alice"));
            Directory.CreateDirectory(Path.Combine(tempRoot, "Users", "Bob"));
            File.WriteAllBytes(Path.Combine(tempRoot, "Users", "Public", "NTUSER.DAT"), [1]);
            File.WriteAllBytes(Path.Combine(tempRoot, "Users", "Alice", "NTUSER.DAT"), [1]);
            File.WriteAllBytes(Path.Combine(tempRoot, "Users", "Bob", "NTUSER.DAT"), [1]);

            var profiles = OfflineTargetContext.EnumerateUserProfiles(winDir);

            Assert.DoesNotContain("Public", profiles, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Alice", profiles, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Bob", profiles, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            try { Directory.Delete(tempRoot, true); } catch { }
        }
    }
}
