using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
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

public record SystemBinaryIntegrityItem(
    string FileName,
    string FullPath,
    bool Exists,
    bool HasIfeoHijack,
    string IfeoTarget,
    string OriginalFilename,
    bool IsSpoofedCmd,
    bool MatchesWinSxS,
    string StatusSummary);

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

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, string pvParam, uint fWinIni);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool MoveFileEx(string lpExistingFileName, string? lpNewFileName, uint dwFlags);

    public Result RestoreDefaultTheme(ITargetContext ctx, BackupManager bm)
    {
        string aeroTheme = Path.Combine(ctx.WindowsDirectory, "Resources", "Themes", "aero.theme");
        string defaultWallpaper = Path.Combine(ctx.WindowsDirectory, "Web", "Wallpaper", "Windows", "img0.jpg");
        string wallpaperValue = File.Exists(defaultWallpaper) ? defaultWallpaper : "";

        // сбрасываем тему на aero.theme
        bm.BackupAndSetRegistryValue(ctx, "HKCU", ThemesPath, "ThemeDir", @"%SystemRoot%\resources\Themes", RegistryValueKind.ExpandString, "Reset ThemeDir");
        bm.BackupAndSetRegistryValue(ctx, "HKCU", ThemesPath, "CurrentTheme", @"%SystemRoot%\resources\Themes\aero.theme", RegistryValueKind.ExpandString, "Reset CurrentTheme to aero.theme");
        bm.BackupAndSetRegistryValue(ctx, "HKCU", ThemesPath, "NewThemeInstalled", 0, RegistryValueKind.DWord, "Reset NewThemeInstalled");

        // сбрасываем ThemeManager и Personalize (светлая тема, прозрачность)
        const string themeManagerPath = @"Software\Microsoft\Windows\CurrentVersion\ThemeManager";
        bm.BackupAndSetRegistryValue(ctx, "HKCU", themeManagerPath, "ThemeActive", "1", RegistryValueKind.String, "Enable ThemeActive");
        bm.BackupAndSetRegistryValue(ctx, "HKCU", themeManagerPath, "DllName", @"%SystemRoot%\resources\Themes\Aero\Aero.msstyles", RegistryValueKind.ExpandString, "Reset Aero.msstyles");
        bm.BackupAndSetRegistryValue(ctx, "HKCU", themeManagerPath, "ColorName", "NormalColor", RegistryValueKind.String, "Reset ColorName");
        bm.BackupAndSetRegistryValue(ctx, "HKCU", themeManagerPath, "SizeName", "NormalSize", RegistryValueKind.String, "Reset SizeName");

        const string personalizePath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        bm.BackupAndSetRegistryValue(ctx, "HKCU", personalizePath, "AppsUseLightTheme", 1, RegistryValueKind.DWord, "Reset AppsUseLightTheme");
        bm.BackupAndSetRegistryValue(ctx, "HKCU", personalizePath, "SystemUsesLightTheme", 1, RegistryValueKind.DWord, "Reset SystemUsesLightTheme");
        bm.BackupAndSetRegistryValue(ctx, "HKCU", personalizePath, "EnableTransparency", 1, RegistryValueKind.DWord, "Enable Transparency");
        bm.BackupAndSetRegistryValue(ctx, "HKCU", personalizePath, "ColorPrevalence", 0, RegistryValueKind.DWord, "Reset ColorPrevalence");

        // отключаем высокую контрастность, если была включена
        const string highContrastPath = @"Control Panel\Accessibility\HighContrast";
        bm.BackupAndSetRegistryValue(ctx, "HKCU", highContrastPath, "Flags", "122", RegistryValueKind.String, "Disable High Contrast");

        // сбрасываем обои
        bm.BackupAndSetRegistryValue(ctx, "HKCU", DesktopPath, "TileWallpaper", "0", RegistryValueKind.String, "Reset TileWallpaper");
        bm.BackupAndSetRegistryValue(ctx, "HKCU", DesktopPath, "WallpaperStyle", "10", RegistryValueKind.String, "Reset WallpaperStyle");
        bm.BackupAndSetRegistryValue(ctx, "HKCU", DesktopPath, "Wallpaper", wallpaperValue, RegistryValueKind.String, "Reset Wallpaper");

        // стандартные цвета окон
        const string colorsPath = @"Control Panel\Colors";
        bm.BackupAndSetRegistryValue(ctx, "HKCU", colorsPath, "Window", "255 255 255", RegistryValueKind.String, "Reset Window color");
        bm.BackupAndSetRegistryValue(ctx, "HKCU", colorsPath, "WindowText", "0 0 0", RegistryValueKind.String, "Reset WindowText color");
        bm.BackupAndSetRegistryValue(ctx, "HKCU", colorsPath, "Background", "0 0 0", RegistryValueKind.String, "Reset Background color");
        bm.BackupAndSetRegistryValue(ctx, "HKCU", colorsPath, "Menu", "240 240 240", RegistryValueKind.String, "Reset Menu color");
        bm.BackupAndSetRegistryValue(ctx, "HKCU", colorsPath, "MenuText", "0 0 0", RegistryValueKind.String, "Reset MenuText color");

        // применяем тему и обои в реальном времени
        if (ctx.IsOnline)
        {
            RunCommandHidden("sc", "config Themes start= auto");
            RunCommandHidden("net", "start Themes");

            try
            {
                const uint SPI_SETDESKWALLPAPER = 0x0014;
                const uint SPIF_UPDATEINIFILE = 0x01;
                const uint SPIF_SENDCHANGE = 0x02;
                SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, wallpaperValue, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

                const uint WM_SETTINGCHANGE = 0x001A;
                const uint SMTO_ABORTIFHUNG = 0x0002;
                SendMessageTimeout(new IntPtr(0xFFFF), WM_SETTINGCHANGE, IntPtr.Zero, "ImmersiveColorSet", SMTO_ABORTIFHUNG, 2000, out _);
                SendMessageTimeout(new IntPtr(0xFFFF), WM_SETTINGCHANGE, IntPtr.Zero, "WindowsThemeElement", SMTO_ABORTIFHUNG, 2000, out _);

                if (File.Exists(aeroTheme))
                {
                    RunCommandHidden("rundll32.exe", $"shell32.dll,Control_RunDLL desk.cpl desk,@Themes /Action:OpenTheme /file:\"{aeroTheme}\"");
                }
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
                    // Если файл уже идентичен эталону — перезапись не нужна
                    if (FilesHaveSameHash(sourcePath, destPath))
                    {
                        if (string.Equals(sourcePath, bakPath, StringComparison.OrdinalIgnoreCase))
                        {
                            try { File.Delete(bakPath); } catch { }
                        }
                        restoredCount++;
                        bm.Log("Accessibility", $"{fileName} already matches reference ({sourcePath})");
                        continue;
                    }

                    if (ctx.IsOnline)
                    {
                        KillProcessesByName(Path.GetFileNameWithoutExtension(fileName));
                    }

                    bm.BackupFile(destPath);
                    fileMgr.TakeOwnershipOfPath(destPath);
                    RunCommandHidden("takeown.exe", $"/f \"{destPath}\" /A");
                    RunCommandHidden("icacls.exe", $"\"{destPath}\" /grant *S-1-5-32-544:F");
                    try { File.SetAttributes(destPath, FileAttributes.Normal); } catch { }
                }

                try
                {
                    File.Copy(sourcePath, destPath, true);
                }
                catch (IOException) when (File.Exists(destPath))
                {
                    // Если файл занят процессом — переименовываем старый файл и ставим на удаление при перезагрузке
                    string oldPath = $"{destPath}.old_{Guid.NewGuid().ToString("N")[..6]}";
                    File.Move(destPath, oldPath, true);
                    MoveFileEx(oldPath, null, 0x4 /* MOVEFILE_DELAY_UNTIL_REBOOT */);
                    File.Copy(sourcePath, destPath, true);
                }

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

    private static bool FilesHaveSameHash(string pathA, string pathB)
    {
        try
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            using var fsA = File.OpenRead(pathA);
            using var fsB = File.OpenRead(pathB);
            byte[] hashA = sha.ComputeHash(fsA);
            byte[] hashB = sha.ComputeHash(fsB);
            return hashA.AsSpan().SequenceEqual(hashB);
        }
        catch
        {
            return false;
        }
    }

    private static void KillProcessesByName(string procName)
    {
        try
        {
            int currentPid = Environment.ProcessId;
            foreach (var p in Process.GetProcessesByName(procName))
            {
                try
                {
                    if (p.Id != currentPid)
                    {
                        p.Kill(entireProcessTree: true);
                        p.WaitForExit(1500);
                    }
                }
                catch { }
                finally { p.Dispose(); }
            }
        }
        catch { }
    }

    private static string? FindInWinSxS(string winSxS, string fileName)
    {
        try
        {
            if (!Directory.Exists(winSxS)) return null;
            // Ищем только в папках компонентов amd64_/x86_, пропуская Manifests/Backup/Catalogs
            foreach (var dir in Directory.EnumerateDirectories(winSxS, "amd64_*"))
            {
                string candidate = Path.Combine(dir, fileName);
                if (File.Exists(candidate)) return candidate;
            }
            foreach (var dir in Directory.EnumerateDirectories(winSxS, "x86_*"))
            {
                string candidate = Path.Combine(dir, fileName);
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, string> BuildWinSxSLookup(string winSxS, IEnumerable<string> fileNames)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(winSxS)) return map;
        var wanted = new HashSet<string>(fileNames, StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var dir in Directory.EnumerateDirectories(winSxS))
            {
                string dirName = Path.GetFileName(dir);
                if (!dirName.StartsWith("amd64_", StringComparison.OrdinalIgnoreCase) &&
                    !dirName.StartsWith("x86_", StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var target in wanted)
                {
                    if (map.ContainsKey(target)) continue;
                    string candidate = Path.Combine(dir, target);
                    if (File.Exists(candidate))
                        map[target] = candidate;
                }

                if (map.Count == wanted.Count) break;
            }
        }
        catch { }

        return map;
    }

    public List<SystemBinaryIntegrityItem> CheckSystemBinariesIntegrity(ITargetContext ctx)
    {
        string[] binaries =
        [
            "sethc.exe", "Utilman.exe", "osk.exe", "Narrator.exe", "Magnify.exe",
            "DisplaySwitch.exe", "AtBroker.exe", "cmd.exe", "explorer.exe",
            "Taskmgr.exe", "regedit.exe", "mmc.exe"
        ];

        string sys32 = ctx.System32Directory;
        string winDir = ctx.WindowsDirectory;
        string winSxS = Path.Combine(winDir, "WinSxS");
        var winSxSMap = BuildWinSxSLookup(winSxS, binaries);
        var fileMgr = new FileManagerService();
        var results = new List<SystemBinaryIntegrityItem>();

        foreach (var bin in binaries)
        {
            string fullPath = bin.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase) ||
                              bin.Equals("regedit.exe", StringComparison.OrdinalIgnoreCase)
                ? (File.Exists(Path.Combine(winDir, bin)) ? Path.Combine(winDir, bin) : Path.Combine(sys32, bin))
                : Path.Combine(sys32, bin);

            bool exists = File.Exists(fullPath);
            string ifeoKey = $@"{IfeoBase}\{bin}";
            string ifeoTarget = ctx.GetValue("HKLM", ifeoKey, "Debugger")?.ToString() ?? "";
            bool hasIfeo = !string.IsNullOrWhiteSpace(ifeoTarget);

            string origName = "";
            bool isSpoofedCmd = false;
            bool matchesWinSxS = true;

            if (exists)
            {
                try
                {
                    var fvi = FileVersionInfo.GetVersionInfo(fullPath);
                    origName = fvi.OriginalFilename ?? "";
                    if (!bin.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase) &&
                        origName.Contains("cmd.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        isSpoofedCmd = true;
                    }
                }
                catch { }

                winSxSMap.TryGetValue(bin, out string? refPath);
                if (refPath != null && File.Exists(refPath))
                {
                    var liveHash = fileMgr.CalculateHashes(fullPath).Sha256;
                    var refHash = fileMgr.CalculateHashes(refPath).Sha256;
                    if (!string.IsNullOrEmpty(liveHash) && !string.IsNullOrEmpty(refHash) && liveHash != "Error")
                    {
                        matchesWinSxS = string.Equals(liveHash, refHash, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }

            string summary = !exists ? "Отсутствует"
                : isSpoofedCmd ? $"ПОДМЕНА НА CMD.EXE ({origName})"
                : hasIfeo ? $"IFEO перехват -> {ifeoTarget}"
                : !matchesWinSxS ? "Не совпадает SHA-256 с WinSxS"
                : "Норма";

            results.Add(new SystemBinaryIntegrityItem(
                bin, fullPath, exists, hasIfeo, ifeoTarget, origName, isSpoofedCmd, matchesWinSxS, summary));
        }

        return results;
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
