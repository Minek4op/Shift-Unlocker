using System.Diagnostics;
using System.Reflection;
using Microsoft.Win32;
using ShiftUnlocker.Core.Common;
using ShiftUnlocker.Core.Context;
using ShiftUnlocker.Core.Modules.FileManager;
using ShiftUnlocker.Core.Safety;

namespace ShiftUnlocker.Core.Modules.SystemRecovery;

public record AccessibilityStatus(
    bool SethcIsReplaced,
    string SethcTarget,
    bool UtilmanIsReplaced,
    string UtilmanTarget);

public class SystemRecoveryService
{
    private const string FontSubstitutesPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\FontSubstitutes";
    private const string FontsPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts";
    private const string CodePagePath = @"SYSTEM\CurrentControlSet\Control\Nls\CodePage";
    private const string LanguagePath = @"SYSTEM\CurrentControlSet\Control\Nls\Language";
    private const string InternationalPath = @"Control Panel\International";
    private const string ThemesPath = @"Software\Microsoft\Windows\CurrentVersion\Themes";
    private const string DesktopPath = @"Control Panel\Desktop";
    private const string IfeoBase = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options";

    public Result FixFonts(ITargetContext ctx, BackupManager bm)
    {
        var errors = new List<string>();

        // сбрасываем FontSubstitutes на стандарт
        var fontSubs = new (string Key, string Value)[]
        {
            ("MS Shell Dlg", "Tahoma"),
            ("MS Shell Dlg 2", "Tahoma"),
            ("Segoe UI", "Segoe UI"),
            ("Arial", "Arial"),
            ("Courier", "Courier New"),
            ("Times", "Times New Roman"),
            ("Helv", "MS Sans Serif"),
            ("Tms Rmn", "MS Serif")
        };

        foreach (var (k, v) in fontSubs)
        {
            var res = bm.BackupAndSetRegistryValue(ctx, "HKLM", FontSubstitutesPath, k, v, RegistryValueKind.String, $"Restored FontSubstitute: {k} -> {v}");
            if (!res.Success && res.ErrorMessage != null) errors.Add(res.ErrorMessage);
        }

        // основные системные шрифты
        var fonts = new (string Name, string File)[]
        {
            ("Segoe UI (TrueType)", "segoeui.ttf"),
            ("Segoe UI Bold (TrueType)", "segoeuib.ttf"),
            ("Segoe UI Italic (TrueType)", "segoeuii.ttf"),
            ("Tahoma (TrueType)", "tahoma.ttf"),
            ("Tahoma Bold (TrueType)", "tahomabd.ttf"),
            ("Arial (TrueType)", "arial.ttf"),
            ("Arial Bold (TrueType)", "arialbd.ttf")
        };

        foreach (var (n, f) in fonts)
        {
            bm.BackupAndSetRegistryValue(ctx, "HKLM", FontsPath, n, f, RegistryValueKind.String, $"Restored Font registration: {n}");
        }

        // чистим кэш шрифтов
        try
        {
            if (ctx.IsOnline)
            {
                RunCommandHidden("net", "stop \"Windows Font Cache Service\" /y");
                string localService = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "ServiceProfiles", "LocalService", "AppData", "Local");
                if (Directory.Exists(localService))
                {
                    foreach (var f in Directory.GetFiles(localService, "FontCache*.dat"))
                    {
                        try { File.Delete(f); } catch { }
                    }
                }
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string gdipCache = Path.Combine(localApp, "GDIPFONTCACHEV1.DAT");
                if (File.Exists(gdipCache))
                {
                    try { File.Delete(gdipCache); } catch { }
                }
                RunCommandHidden("net", "start \"Windows Font Cache Service\"");
            }
            else
            {
                string localService = Path.Combine(ctx.WindowsDirectory, "ServiceProfiles", "LocalService", "AppData", "Local");
                if (Directory.Exists(localService))
                {
                    foreach (var f in Directory.GetFiles(localService, "FontCache*.dat"))
                    {
                        try { File.Delete(f); } catch { }
                    }
                }
            }
        }
        catch { }

        bm.Log("SystemRecovery", "Fixed system fonts and font substitutes");
        return errors.Count == 0 ? Result.Ok() : Result.Fail("Ошибки при восстановлении некоторых шрифтов: " + string.Join("; ", errors));
    }

    public Result RestoreDefaultTheme(ITargetContext ctx, BackupManager bm)
    {
        // сбрасываем тему на aero.theme
        bm.BackupAndSetRegistryValue(ctx, "HKCU", ThemesPath, "ThemeDir", @"%SystemRoot%\resources\Themes", RegistryValueKind.ExpandString, "Reset ThemeDir");
        bm.BackupAndSetRegistryValue(ctx, "HKCU", ThemesPath, "CurrentTheme", @"%SystemRoot%\resources\Themes\aero.theme", RegistryValueKind.ExpandString, "Reset CurrentTheme to aero.theme");
        bm.BackupAndSetRegistryValue(ctx, "HKCU", ThemesPath, "NewThemeInstalled", 0, RegistryValueKind.DWord, "Reset NewThemeInstalled");

        // сбрасываем обои
        bm.BackupAndSetRegistryValue(ctx, "HKCU", DesktopPath, "TileWallpaper", "0", RegistryValueKind.String, "Reset TileWallpaper");
        bm.BackupAndSetRegistryValue(ctx, "HKCU", DesktopPath, "WallpaperStyle", "2", RegistryValueKind.String, "Reset WallpaperStyle");
        bm.BackupAndSetRegistryValue(ctx, "HKCU", DesktopPath, "Wallpaper", "", RegistryValueKind.String, "Cleared Wallpaper");

        // стандартные цвета окон
        const string colorsPath = @"Control Panel\Colors";
        bm.BackupAndSetRegistryValue(ctx, "HKCU", colorsPath, "Window", "255 255 255", RegistryValueKind.String, "Reset Window color");
        bm.BackupAndSetRegistryValue(ctx, "HKCU", colorsPath, "WindowText", "0 0 0", RegistryValueKind.String, "Reset WindowText color");
        bm.BackupAndSetRegistryValue(ctx, "HKCU", colorsPath, "Background", "0 0 0", RegistryValueKind.String, "Reset Background color");
        bm.BackupAndSetRegistryValue(ctx, "HKCU", colorsPath, "Menu", "240 240 240", RegistryValueKind.String, "Reset Menu color");
        bm.BackupAndSetRegistryValue(ctx, "HKCU", colorsPath, "MenuText", "0 0 0", RegistryValueKind.String, "Reset MenuText color");

        // запускаем службу Themes
        if (ctx.IsOnline)
        {
            RunCommandHidden("sc", "config Themes start= auto");
            RunCommandHidden("net", "start Themes");
            try
            {
                RunCommandHidden("rundll32.exe", "themeui.dll,ThemeChangeNotify");
            }
            catch { }
        }

        bm.Log("SystemRecovery", "Restored default Windows theme (aero.theme)");
        return Result.Ok();
    }

    public Result RestoreRussianLanguage(ITargetContext ctx, BackupManager bm)
    {
        var errors = new List<string>();

        // возвращаем 1251/866 и фиксим 1252 от кракозябр
        var codePages = new (string Key, string Value)[]
        {
            ("1250", "c_1251.nls"),
            ("1251", "c_1251.nls"),
            ("1252", "c_1251.nls"),
            ("ACP", "1251"),
            ("OEMCP", "866"),
            ("MACCP", "10007")
        };

        foreach (var (k, v) in codePages)
        {
            var res = bm.BackupAndSetRegistryValue(ctx, "HKLM", CodePagePath, k, v, RegistryValueKind.String, $"Restored CodePage {k} -> {v}");
            if (!res.Success && res.ErrorMessage != null) errors.Add(res.ErrorMessage);
        }

        // код русского языка 0419
        bm.BackupAndSetRegistryValue(ctx, "HKLM", LanguagePath, "Default", "0419", RegistryValueKind.String, "Set Default Language to 0419");
        bm.BackupAndSetRegistryValue(ctx, "HKLM", LanguagePath, "InstallLanguage", "0419", RegistryValueKind.String, "Set InstallLanguage to 0419");

        // региональные параметры текущего юзера
        bm.BackupAndSetRegistryValue(ctx, "HKCU", InternationalPath, "Locale", "00000419", RegistryValueKind.String, "Set Locale to Russian 0419");
        bm.BackupAndSetRegistryValue(ctx, "HKCU", InternationalPath, "LocaleName", "ru-RU", RegistryValueKind.String, "Set LocaleName to ru-RU");
        bm.BackupAndSetRegistryValue(ctx, "HKCU", InternationalPath, "sLanguage", "RUS", RegistryValueKind.String, "Set sLanguage to RUS");
        bm.BackupAndSetRegistryValue(ctx, "HKCU", InternationalPath, "sCountry", "Россия", RegistryValueKind.String, "Set sCountry to Россия");

        bm.Log("SystemRecovery", "Restored Russian language (CP1251, 866, 0419)");
        return errors.Count == 0 ? Result.Ok() : Result.Fail("Ошибки при настройке кодовых страниц: " + string.Join("; ", errors));
    }

    public AccessibilityStatus CheckAccessibilityStatus(ITargetContext ctx)
    {
        string sethcIfeo = $@"{IfeoBase}\sethc.exe";
        string utilmanIfeo = $@"{IfeoBase}\utilman.exe";

        string? sethcDeb = ctx.GetValue("HKLM", sethcIfeo, "Debugger")?.ToString();
        string? utilmanDeb = ctx.GetValue("HKLM", utilmanIfeo, "Debugger")?.ToString();

        bool sethcReplaced = !string.IsNullOrWhiteSpace(sethcDeb);
        string sethcTarget = sethcDeb ?? "Штатный (sethc.exe)";

        bool utilmanReplaced = !string.IsNullOrWhiteSpace(utilmanDeb);
        string utilmanTarget = utilmanDeb ?? "Штатный (utilman.exe)";

        // смотрим также .bak файлы в System32
        string sys32 = ctx.System32Directory;
        string sethcBak = Path.Combine(sys32, "sethc.exe.bak");
        string utilmanBak = Path.Combine(sys32, "utilman.exe.bak");

        if (File.Exists(sethcBak))
        {
            sethcReplaced = true;
            if (string.IsNullOrWhiteSpace(sethcDeb)) sethcTarget = "Файл заменен (есть sethc.exe.bak)";
        }
        else if (!File.Exists(Path.Combine(sys32, "sethc.exe")))
        {
            sethcReplaced = true;
            sethcTarget = "Файл отсутствует в System32!";
        }

        if (File.Exists(utilmanBak))
        {
            utilmanReplaced = true;
            if (string.IsNullOrWhiteSpace(utilmanDeb)) utilmanTarget = "Файл заменен (есть utilman.exe.bak)";
        }
        else if (!File.Exists(Path.Combine(sys32, "Utilman.exe")))
        {
            utilmanReplaced = true;
            utilmanTarget = "Файл отсутствует в System32!";
        }

        return new AccessibilityStatus(sethcReplaced, sethcTarget, utilmanReplaced, utilmanTarget);
    }

    public Result ReplaceSethcWithCmd(ITargetContext ctx, BackupManager bm)
    {
        string key = $@"{IfeoBase}\sethc.exe";
        string cmdPath = Path.Combine(ctx.System32Directory, "cmd.exe");
        return bm.BackupAndSetRegistryValue(ctx, "HKLM", key, "Debugger", cmdPath, RegistryValueKind.String, "Replaced sethc.exe with cmd.exe via IFEO");
    }

    public Result ReplaceSethcWithApp(ITargetContext ctx, BackupManager bm, string appPath)
    {
        string key = $@"{IfeoBase}\sethc.exe";
        return bm.BackupAndSetRegistryValue(ctx, "HKLM", key, "Debugger", appPath, RegistryValueKind.String, "Replaced sethc.exe with Shift Unlocker via IFEO");
    }

    public Result ReplaceUtilmanWithCmd(ITargetContext ctx, BackupManager bm)
    {
        string key = $@"{IfeoBase}\utilman.exe";
        string cmdPath = Path.Combine(ctx.System32Directory, "cmd.exe");
        return bm.BackupAndSetRegistryValue(ctx, "HKLM", key, "Debugger", cmdPath, RegistryValueKind.String, "Replaced utilman.exe with cmd.exe via IFEO");
    }

    public Result ReplaceUtilmanWithApp(ITargetContext ctx, BackupManager bm, string appPath)
    {
        string key = $@"{IfeoBase}\utilman.exe";
        return bm.BackupAndSetRegistryValue(ctx, "HKLM", key, "Debugger", appPath, RegistryValueKind.String, "Replaced utilman.exe with Shift Unlocker via IFEO");
    }

    public Result RestoreOriginalAccessibility(ITargetContext ctx, BackupManager bm)
    {
        // убираем отладчики IFEO
        bm.BackupAndDeleteRegistryValue(ctx, "HKLM", $@"{IfeoBase}\sethc.exe", "Debugger", "Restored sethc.exe IFEO");
        bm.BackupAndDeleteRegistryValue(ctx, "HKLM", $@"{IfeoBase}\utilman.exe", "Debugger", "Restored utilman.exe IFEO");

        // возвращаем .bak если сохранились
        string sys32 = ctx.System32Directory;
        string sethcBak = Path.Combine(sys32, "sethc.exe.bak");
        string sethc = Path.Combine(sys32, "sethc.exe");
        if (File.Exists(sethcBak))
        {
            try
            {
                File.Copy(sethcBak, sethc, true);
                File.Delete(sethcBak);
                bm.Log("Accessibility", "Restored sethc.exe from backup");
            }
            catch { }
        }

        string utilmanBak = Path.Combine(sys32, "utilman.exe.bak");
        string utilman = Path.Combine(sys32, "utilman.exe");
        if (File.Exists(utilmanBak))
        {
            try
            {
                File.Copy(utilmanBak, utilman, true);
                File.Delete(utilmanBak);
                bm.Log("Accessibility", "Restored utilman.exe from backup");
            }
            catch { }
        }

        return Result.Ok();
    }

    public Result RestoreAccessibilityFilesFromWinSxS(ITargetContext ctx, BackupManager bm)
    {
        // сначала чистим IFEO
        bm.BackupAndDeleteRegistryValue(ctx, "HKLM", $@"{IfeoBase}\sethc.exe", "Debugger", "Removed sethc.exe IFEO Debugger");
        bm.BackupAndDeleteRegistryValue(ctx, "HKLM", $@"{IfeoBase}\utilman.exe", "Debugger", "Removed utilman.exe IFEO Debugger");

        string sys32 = ctx.System32Directory;
        string winSxS = Path.Combine(ctx.WindowsDirectory, "WinSxS");
        var fileMgr = new FileManagerService();
        var errors = new List<string>();
        int restoredCount = 0;

        string[] targets = ["sethc.exe", "Utilman.exe"];
        foreach (var fileName in targets)
        {
            string destPath = Path.Combine(sys32, fileName);
            string bakPath = destPath + ".bak";
            string? sourcePath = File.Exists(bakPath) ? bakPath : FindInWinSxS(winSxS, fileName);

            if (sourcePath == null || !File.Exists(sourcePath))
            {
                errors.Add($"{fileName}: не найден ни .bak, ни эталон в WinSxS");
                continue;
            }

            try
            {
                if (File.Exists(destPath))
                {
                    bm.BackupFile(destPath);
                    fileMgr.TakeOwnershipOfPath(destPath);
                    RunCommandHidden("takeown.exe", $"/f \"{destPath}\" /A");
                    RunCommandHidden("icacls.exe", $"\"{destPath}\" /grant *S-1-5-32-544:F");
                    try { File.SetAttributes(destPath, FileAttributes.Normal); } catch { }
                }

                File.Copy(sourcePath, destPath, true);
                if (string.Equals(sourcePath, bakPath, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(bakPath); } catch { }
                }
                restoredCount++;
                bm.Log("Accessibility", $"Restored {fileName} from {sourcePath} to {destPath}");
            }
            catch (Exception ex)
            {
                errors.Add($"{fileName}: {ex.Message}");
            }
        }

        if (errors.Count > 0)
        {
            return Result.Fail($"Восстановлено файлов: {restoredCount}. Ошибки: {string.Join("; ", errors)}");
        }

        return Result.Ok();
    }

    private static string? FindInWinSxS(string winSxS, string fileName)
    {
        try
        {
            if (!Directory.Exists(winSxS)) return null;
            return Directory.EnumerateFiles(winSxS, fileName, SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    public Result RunSfcScan(ITargetContext ctx)
    {
        try
        {
            string args;
            if (ctx.IsOnline)
            {
                args = "/k sfc /scannow";
            }
            else
            {
                args = $"/k sfc /scannow /offbootdir=\"{ctx.TargetRootPath}\" /offwindir=\"{ctx.WindowsDirectory}\"";
            }

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = args,
                UseShellExecute = true,
                Verb = "runas"
            };
            Process.Start(psi);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail($"Ошибка запуска SFC: {ex.Message}");
        }
    }

    public Result LogoffCurrentUser()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "shutdown.exe",
                Arguments = "/l /f",
                UseShellExecute = true
            };
            Process.Start(psi);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail($"Ошибка выхода из пользователя: {ex.Message}");
        }
    }

    private static void RunCommandHidden(string exe, string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(5000);
        }
        catch { }
    }
}
