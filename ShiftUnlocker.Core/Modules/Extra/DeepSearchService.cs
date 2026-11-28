using System.Text.Json;
using System.Text.RegularExpressions;
using ShiftUnlocker.Core.Context;

namespace ShiftUnlocker.Core.Modules.Extra;

public record DeepSearchPattern(string Name, string RegexPattern, bool IsCritical);

public record DeepSearchMatch(
    string SourceType,
    string Location,
    string ValueName,
    string MatchedText,
    string PatternName,
    bool IsCritical);

public class DeepSearchService
{
    private readonly string _patternsPath;

    public DeepSearchService(string? baseDir = null)
    {
        string root = baseDir ?? AppDomain.CurrentDomain.BaseDirectory;
        _patternsPath = Path.Combine(root, "deep_search_patterns.json");
    }

    public List<DeepSearchPattern> LoadPatterns()
    {
        if (File.Exists(_patternsPath))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<List<DeepSearchPattern>>(File.ReadAllText(_patternsPath));
                if (loaded != null && loaded.Count > 0) return loaded;
            }
            catch { }
        }

        return
        [
            new("Encoded PowerShell", @"powershell(\.exe)?\s+.*(-enc|-encodedcommand)", true),
            new("MSHTA Execution", @"mshta(\.exe)?\s+.*", true),
            new("Certutil Download", @"certutil(\.exe)?\s+.*(-urlcache|-split)", true),
            new("Bitsadmin Transfer", @"bitsadmin(\.exe)?\s+.*(/transfer|/addfile)", true),
            new("Regsvr32 Remote", @"regsvr32(\.exe)?\s+.*/i:http", true),
            new("Base64 Payload", @"(FromBase64String|[A-Za-z0-9+/]{64,}={0,2})", true),
            new("Temp/AppData Exec", @"(\\AppData\\Local\\Temp\\|\\Windows\\Temp\\).*\.(exe|bat|vbs|ps1|js)", false)
        ];
    }

    public List<DeepSearchMatch> SearchRegistryAndDisk(
        ITargetContext ctx,
        bool searchRegistry = true,
        bool searchDisk = true,
        CancellationToken ct = default)
    {
        var matches = new List<DeepSearchMatch>();
        var compiled = LoadPatterns()
            .Select(p => (p.Name, p.IsCritical, Rx: new Regex(p.RegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)))
            .ToList();

        if (searchRegistry)
        {
            var keys = new (string Hive, string Key)[]
            {
                ("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Run"),
                ("HKCU", @"Software\Microsoft\Windows\CurrentVersion\RunOnce"),
                ("HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"),
                ("HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce"),
                ("HKLM", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon"),
                ("HKCU", @"Environment")
            };

            foreach (var (hive, key) in keys)
            {
                if (ct.IsCancellationRequested) break;
                foreach (var valName in ctx.GetValueNames(hive, key))
                {
                    string val = ctx.GetValue(hive, key, valName)?.ToString() ?? "";
                    if (string.IsNullOrWhiteSpace(val)) continue;
                    foreach (var p in compiled)
                    {
                        if (p.Rx.IsMatch(val))
                        {
                            matches.Add(new DeepSearchMatch("Реестр", $@"{hive}\{key}", valName, val, p.Name, p.IsCritical));
                            break;
                        }
                    }
                }
            }
        }

        if (searchDisk)
        {
            var scanDirs = new List<string>
            {
                Path.Combine(ctx.WindowsDirectory, "Temp"),
                Path.Combine(ctx.TargetRootPath, "ProgramData")
            };
            string usersDir = Path.Combine(ctx.TargetRootPath, "Users");
            if (Directory.Exists(usersDir))
            {
                foreach (var u in Directory.GetDirectories(usersDir))
                {
                    scanDirs.Add(Path.Combine(u, @"AppData\Local\Temp"));
                    scanDirs.Add(Path.Combine(u, @"AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup"));
                }
            }

            string[] scriptExts = [".bat", ".cmd", ".ps1", ".vbs", ".js", ".hta"];
            foreach (var dir in scanDirs.Where(Directory.Exists))
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    foreach (var file in Directory.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly))
                    {
                        if (ct.IsCancellationRequested) break;
                        foreach (var p in compiled)
                        {
                            if (p.Rx.IsMatch(file))
                            {
                                matches.Add(new DeepSearchMatch("Диск (путь)", file, Path.GetFileName(file), file, p.Name, p.IsCritical));
                                break;
                            }
                        }

                        if (scriptExts.Contains(Path.GetExtension(file).ToLowerInvariant()) && new FileInfo(file).Length <= 256 * 1024)
                        {
                            try
                            {
                                string text = File.ReadAllText(file);
                                foreach (var p in compiled)
                                {
                                    if (p.Rx.IsMatch(text))
                                    {
                                        matches.Add(new DeepSearchMatch("Диск (скрипт)", file, Path.GetFileName(file), file, p.Name, p.IsCritical));
                                        break;
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }
        }

        return matches;
    }
}
