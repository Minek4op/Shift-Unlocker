using System.Text.Json;
using ShiftUnlocker.Core.Common;
using ShiftUnlocker.Core.Context;
using ShiftUnlocker.Core.Safety;

namespace ShiftUnlocker.Core.Modules.Browsers;

public record BrowserFinding(
    string Browser,
    string Category,
    string Title,
    string Details,
    string Location,
    bool IsSuspicious,
    Func<ITargetContext, BackupManager, Result>? FixAction = null);

public class BrowserAuditService
{
    public List<BrowserFinding> ScanBrowsers(ITargetContext ctx)
    {
        var list = new List<BrowserFinding>();

        // 1. Browser Policies (Chrome, Edge, Brave, Opera, Yandex, Firefox)
        ScanBrowserPolicies(ctx, list);

        // 2. Chromium-based profiles (Extensions & Homepage — NO passwords/cookies!)
        ScanChromiumProfiles(ctx, list);

        // 3. Firefox profiles (extensions.json / prefs.js)
        ScanFirefoxProfiles(ctx, list);

        // 4. Browser shortcuts (.lnk) with hijacked URL or --load-extension
        ScanBrowserShortcuts(ctx, list);

        return list;
    }

    private static void ScanBrowserPolicies(ITargetContext ctx, List<BrowserFinding> list)
    {
        var policyRoots = new (string Browser, string SubKey)[]
        {
            ("Google Chrome", @"SOFTWARE\Policies\Google\Chrome"),
            ("Microsoft Edge", @"SOFTWARE\Policies\Microsoft\Edge"),
            ("Brave", @"SOFTWARE\Policies\BraveSoftware\Brave"),
            ("Yandex Browser", @"SOFTWARE\Policies\Yandex\YandexBrowser"),
            ("Opera", @"SOFTWARE\Policies\Opera Software"),
            ("Mozilla Firefox", @"SOFTWARE\Policies\Mozilla\Firefox")
        };

        foreach (var (browser, baseKey) in policyRoots)
        {
            foreach (var hive in new[] { "HKLM", "HKCU" })
            {
                // ExtensionInstallForcelist
                string forceKey = $@"{baseKey}\ExtensionInstallForcelist";
                foreach (var valName in ctx.GetValueNames(hive, forceKey))
                {
                    string val = ctx.GetValue(hive, forceKey, valName)?.ToString() ?? "";
                    string capturedHive = hive;
                    string capturedKey = forceKey;
                    string capturedVal = valName;
                    list.Add(new BrowserFinding(
                        browser,
                        "Принудительное расширение",
                        $"{valName}: {val}",
                        $"Принудительная установка расширения через политику ({capturedHive}\\{capturedKey})",
                        $"{capturedHive}\\{capturedKey}",
                        true,
                        (c, bm) => bm.BackupAndDeleteRegistryValue(c, capturedHive, capturedKey, capturedVal, $"Removed {browser} forced extension {capturedVal}")));
                }

                // HomepageLocation / ProxySettings / DefaultSearchProviderSearchURL
                string[] suspectPols = ["HomepageLocation", "NewTabPageLocation", "DefaultSearchProviderSearchURL", "ProxyServer", "ProxyPacUrl"];
                foreach (var pol in suspectPols)
                {
                    string? v = ctx.GetValue(hive, baseKey, pol)?.ToString();
                    if (string.IsNullOrWhiteSpace(v)) continue;
                    string capturedHive = hive;
                    string capturedKey = baseKey;
                    string capturedPol = pol;
                    list.Add(new BrowserFinding(
                        browser,
                        "Политика браузера",
                        $"{pol} = {v}",
                        $"Задана политика браузера в {capturedHive}\\{capturedKey}",
                        $"{capturedHive}\\{capturedKey}",
                        true,
                        (c, bm) => bm.BackupAndDeleteRegistryValue(c, capturedHive, capturedKey, capturedPol, $"Removed {browser} policy {capturedPol}")));
                }
            }
        }
    }

    private static void ScanChromiumProfiles(ITargetContext ctx, List<BrowserFinding> list)
    {
        var browserRelPaths = new (string Browser, string RelLocalAppData)[]
        {
            ("Google Chrome", @"Google\Chrome\User Data"),
            ("Microsoft Edge", @"Microsoft\Edge\User Data"),
            ("Brave", @"BraveSoftware\Brave-Browser\User Data"),
            ("Yandex Browser", @"Yandex\YandexBrowser\User Data"),
            ("Opera", @"Opera Software\Opera Stable")
        };

        string usersDir = Path.Combine(ctx.TargetRootPath, "Users");
        if (!Directory.Exists(usersDir)) return;

        foreach (var uDir in Directory.GetDirectories(usersDir))
        {
            string localApp = Path.Combine(uDir, @"AppData\Local");
            if (!Directory.Exists(localApp)) continue;

            foreach (var (browser, rel) in browserRelPaths)
            {
                string userData = Path.Combine(localApp, rel);
                if (!Directory.Exists(userData)) continue;

                // Scan Default & Profile *
                var profiles = new List<string> { Path.Combine(userData, "Default"), userData };
                try { profiles.AddRange(Directory.GetDirectories(userData, "Profile *")); } catch { }

                foreach (var prof in profiles.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    string extDir = Path.Combine(prof, "Extensions");
                    if (Directory.Exists(extDir))
                    {
                        try
                        {
                            foreach (var extIdDir in Directory.GetDirectories(extDir))
                            {
                                string extId = Path.GetFileName(extIdDir);
                                string extName = ReadChromiumExtensionName(extIdDir) ?? extId;
                                list.Add(new BrowserFinding(
                                    browser,
                                    "Расширение",
                                    $"{extName} ({extId})",
                                    $"Установлено в {Path.GetFileName(prof)}",
                                    extIdDir,
                                    false));
                            }
                        }
                        catch { }
                    }

                    string prefsPath = Path.Combine(prof, "Preferences");
                    if (File.Exists(prefsPath))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(File.ReadAllText(prefsPath));
                            if (doc.RootElement.TryGetProperty("homepage", out var hp))
                            {
                                string hpStr = hp.GetString() ?? "";
                                if (!string.IsNullOrWhiteSpace(hpStr))
                                {
                                    list.Add(new BrowserFinding(browser, "Домашняя страница", hpStr, $"Профиль {Path.GetFileName(prof)}", prefsPath, false));
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
        }
    }

    private static string? ReadChromiumExtensionName(string extIdDir)
    {
        try
        {
            foreach (var verDir in Directory.GetDirectories(extIdDir))
            {
                string manifest = Path.Combine(verDir, "manifest.json");
                if (!File.Exists(manifest)) continue;
                using var doc = JsonDocument.Parse(File.ReadAllText(manifest));
                if (doc.RootElement.TryGetProperty("name", out var n))
                {
                    string name = n.GetString() ?? "";
                    if (!name.StartsWith("__MSG_", StringComparison.Ordinal)) return name;
                }
            }
        }
        catch { }
        return null;
    }

    private static void ScanFirefoxProfiles(ITargetContext ctx, List<BrowserFinding> list)
    {
        string usersDir = Path.Combine(ctx.TargetRootPath, "Users");
        if (!Directory.Exists(usersDir)) return;

        foreach (var uDir in Directory.GetDirectories(usersDir))
        {
            string ffProfiles = Path.Combine(uDir, @"AppData\Roaming\Mozilla\Firefox\Profiles");
            if (!Directory.Exists(ffProfiles)) continue;

            foreach (var prof in Directory.GetDirectories(ffProfiles))
            {
                string extJson = Path.Combine(prof, "extensions.json");
                if (File.Exists(extJson))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(File.ReadAllText(extJson));
                        if (doc.RootElement.TryGetProperty("addons", out var addons) && addons.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var a in addons.EnumerateArray())
                            {
                                string id = a.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
                                string name = "";
                                if (a.TryGetProperty("defaultLocale", out var loc) && loc.TryGetProperty("name", out var nEl))
                                    name = nEl.GetString() ?? "";
                                if (!string.IsNullOrWhiteSpace(id))
                                    list.Add(new BrowserFinding("Mozilla Firefox", "Расширение", $"{name} ({id})", Path.GetFileName(prof), extJson, false));
                            }
                        }
                    }
                    catch { }
                }

                string prefsJs = Path.Combine(prof, "prefs.js");
                if (File.Exists(prefsJs))
                {
                    try
                    {
                        foreach (var line in File.ReadLines(prefsJs))
                        {
                            if (line.Contains("browser.startup.homepage", StringComparison.OrdinalIgnoreCase) ||
                                line.Contains("network.proxy.autoconfig_url", StringComparison.OrdinalIgnoreCase))
                            {
                                list.Add(new BrowserFinding("Mozilla Firefox", "Настройка prefs.js", line.Trim(), Path.GetFileName(prof), prefsJs, line.Contains("proxy", StringComparison.OrdinalIgnoreCase)));
                            }
                        }
                    }
                    catch { }
                }
            }
        }
    }

    private static void ScanBrowserShortcuts(ITargetContext ctx, List<BrowserFinding> list)
    {
        var dirs = new List<string>
        {
            Path.Combine(ctx.TargetRootPath, @"Users\Public\Desktop"),
            Path.Combine(ctx.TargetRootPath, @"ProgramData\Microsoft\Windows\Start Menu\Programs")
        };

        string usersDir = Path.Combine(ctx.TargetRootPath, "Users");
        if (Directory.Exists(usersDir))
        {
            foreach (var u in Directory.GetDirectories(usersDir))
                dirs.Add(Path.Combine(u, "Desktop"));
        }

        string[] browserNames = ["chrome", "msedge", "firefox", "opera", "browser", "brave"];
        foreach (var d in dirs.Where(Directory.Exists))
        {
            try
            {
                foreach (var lnk in Directory.GetFiles(d, "*.lnk", SearchOption.AllDirectories))
                {
                    string fn = Path.GetFileNameWithoutExtension(lnk).ToLowerInvariant();
                    if (!browserNames.Any(b => fn.Contains(b))) continue;

                    // Читаем ASCII/UTF-16 строки из .lnk без COM-зависимостей (совместимо с WinPE)
                    byte[] raw = File.ReadAllBytes(lnk);
                    string ascii = System.Text.Encoding.ASCII.GetString(raw);
                    string utf16 = System.Text.Encoding.Unicode.GetString(raw);
                    bool hasInjectedArg = ascii.Contains("--load-extension", StringComparison.OrdinalIgnoreCase) ||
                                          utf16.Contains("--load-extension", StringComparison.OrdinalIgnoreCase) ||
                                          ascii.Contains("http://", StringComparison.OrdinalIgnoreCase) ||
                                          ascii.Contains("https://", StringComparison.OrdinalIgnoreCase) ||
                                          utf16.Contains("http://", StringComparison.OrdinalIgnoreCase) ||
                                          utf16.Contains("https://", StringComparison.OrdinalIgnoreCase);

                    if (hasInjectedArg)
                    {
                        list.Add(new BrowserFinding(
                            "Ярлык (.lnk)",
                            "Подмена ярлыка",
                            Path.GetFileName(lnk),
                            "В ярлыке браузера обнаружен посторонний URL или --load-extension",
                            lnk,
                            true));
                    }
                }
            }
            catch { }
        }
    }
}
