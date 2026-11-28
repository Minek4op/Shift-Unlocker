using System.Diagnostics;
using System.Text.Json;
using ShiftUnlocker.Core.Context;

namespace ShiftUnlocker.Core.Modules.Extra;

public record RansomwareAnalysisReport(
    List<string> RansomNotesFound,
    Dictionary<string, int> SuspiciousExtensionCounts,
    List<string> DetectedFamilies,
    string ShadowCopiesOutput);

public class RansomwareHelperService
{
    private readonly string _dbPath;

    public RansomwareHelperService(string? baseDir = null)
    {
        string root = baseDir ?? AppDomain.CurrentDomain.BaseDirectory;
        _dbPath = Path.Combine(root, "ransomware_extensions.json");
    }

    public Dictionary<string, string> LoadExtensionDatabase()
    {
        if (File.Exists(_dbPath))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_dbPath));
                if (loaded != null && loaded.Count > 0)
                    return new Dictionary<string, string>(loaded, StringComparer.OrdinalIgnoreCase);
            }
            catch { }
        }

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".lockbit"] = "LockBit",
            [".akira"] = "Akira",
            [".play"] = "Play",
            [".ryuk"] = "Ryuk",
            [".conti"] = "Conti",
            [".wannacry"] = "WannaCry",
            [".wncry"] = "WannaCry",
            [".djvu"] = "STOP/Djvu",
            [".phobos"] = "Phobos",
            [".makop"] = "Makop",
            [".encrypted"] = "Generic Ransomware"
        };
    }

    public RansomwareAnalysisReport AnalyzeUserFiles(ITargetContext ctx)
    {
        var db = LoadExtensionDatabase();
        var notes = new List<string>();
        var extCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var families = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string[] noteMarkers = ["readme", "decrypt", "restore_files", "how_to", "recover"];
        string usersDir = Path.Combine(ctx.TargetRootPath, "Users");

        if (Directory.Exists(usersDir))
        {
            foreach (var uDir in Directory.GetDirectories(usersDir))
            {
                foreach (var sub in new[] { "Desktop", "Documents", "Downloads" })
                {
                    string target = Path.Combine(uDir, sub);
                    if (!Directory.Exists(target)) continue;

                    try
                    {
                        foreach (var file in Directory.EnumerateFiles(target, "*.*", SearchOption.TopDirectoryOnly))
                        {
                            string fn = Path.GetFileName(file).ToLowerInvariant();
                            string ext = Path.GetExtension(file);

                            if ((ext.Equals(".txt", StringComparison.OrdinalIgnoreCase) || ext.Equals(".html", StringComparison.OrdinalIgnoreCase)) &&
                                noteMarkers.Any(m => fn.Contains(m)))
                            {
                                notes.Add(file);
                            }

                            if (!string.IsNullOrEmpty(ext) && db.TryGetValue(ext, out string? fam))
                            {
                                extCounts[ext] = extCounts.GetValueOrDefault(ext, 0) + 1;
                                families.Add(fam);
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        string vssOut = "";
        if (ctx.IsOnline)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "vssadmin.exe",
                    Arguments = "list shadows",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                if (p != null)
                {
                    vssOut = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                    p.WaitForExit(5000);
                }
            }
            catch { }
        }

        return new RansomwareAnalysisReport(notes, extCounts, families.ToList(), vssOut.Trim());
    }
}
