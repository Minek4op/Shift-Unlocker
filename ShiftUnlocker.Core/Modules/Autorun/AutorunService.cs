using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using ShiftUnlocker.Core.Common;
using ShiftUnlocker.Core.Context;
using ShiftUnlocker.Core.Safety;

namespace ShiftUnlocker.Core.Modules.Autorun;

public class AutorunService
{
    private static readonly (string Hive, string KeyPath, string Category)[] RegistryRunLocations =
    [
        ("HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "HKLM Run"),
        ("HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce", "HKLM RunOnce"),
        ("HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunServices", "HKLM RunServices"),
        ("HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnceEx", "HKLM RunOnceEx"),
        ("HKLM", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "HKLM Run (32-bit)"),
        ("HKLM", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\RunOnce", "HKLM RunOnce (32-bit)"),
        ("HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer\Run", "HKLM Policies Run"),
        ("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Run", "HKCU Run"),
        ("HKCU", @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "HKCU RunOnce"),
        ("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer\Run", "HKCU Policies Run")
    ];

    public async Task<List<AutorunEntry>> ScanAllAutorunsAsync(ITargetContext ctx, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var list = new List<AutorunEntry>();

            // ветки Run / RunOnce (активные и отключенные)
            foreach (var loc in RegistryRunLocations)
            {
                if (ct.IsCancellationRequested) break;
                ScanRegistryKey(ctx, loc.Hive, loc.KeyPath, loc.Category, true, list);

                // отключенные нашей прогой записи
                string disabledKey = loc.KeyPath + "_ShiftUnlockerDisabled";
                ScanRegistryKey(ctx, loc.Hive, disabledKey, loc.Category + " (Отключено)", false, list);
            }

            // проверяем Winlogon
            ScanWinlogon(ctx, list);

            // проверяем BootExecute
            ScanBootExecute(ctx, list);

            // перехваты IFEO
            ScanIfeoDebuggers(ctx, list);

            // папки автозагрузки
            ScanStartupFolders(ctx, list);

            // планировщик задач (xml в System32\Tasks)
            ScanScheduledTasks(ctx, list);

            // расширенные точки автозапуска (COM, AppCompat, TimeProviders, PrintMonitors, Winsock LSP, PS-профили, ENV)
            ScanComHijacks(ctx, list);
            ScanAppCompatShims(ctx, list);
            ScanTimeProviders(ctx, list);
            ScanPrintMonitors(ctx, list);
            ScanWinsockLsp(ctx, list);
            ScanPowerShellProfiles(ctx, list);
            ScanSuspiciousEnvVars(ctx, list);

            return DeduplicateEntries(list);
        }, ct);
    }

    private static readonly HashSet<string> GenericHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "cmd.exe", "powershell.exe", "pwsh.exe", "wscript.exe", "cscript.exe",
        "mshta.exe", "rundll32.exe", "regsvr32.exe", "svchost.exe", "conhost.exe"
    };

    /// <summary>
    /// Убирает дубликаты записей автозагрузки по нормализованному пути файла
    /// (или полной команде для системных интерпретаторов), сохраняя первый приоритетный источник.
    /// </summary>
    public static List<AutorunEntry> DeduplicateEntries(IEnumerable<AutorunEntry> entries)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<AutorunEntry>();

        foreach (var entry in entries)
        {
            string key = BuildDedupKey(entry);
            if (seen.Add(key))
            {
                result.Add(entry);
            }
        }

        return result;
    }

    private static string BuildDedupKey(AutorunEntry entry)
    {
        string resolved = (entry.ResolvedFilePath ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(resolved))
        {
            string fileName = Path.GetFileName(resolved);
            if (GenericHosts.Contains(fileName))
            {
                string normCmd = Regex.Replace((entry.Command ?? resolved).Trim(), @"\s+", " ");
                return $"CMD:{normCmd}";
            }

            try
            {
                resolved = Path.GetFullPath(resolved);
            }
            catch
            {
                // оставляем исходный путь при недопустимых символах
            }

            return $"FILE:{resolved}";
        }

        string cmd = Regex.Replace((entry.Command ?? entry.Name ?? entry.Id).Trim(), @"\s+", " ");
        return $"CMD:{cmd}";
    }

    private void ScanRegistryKey(ITargetContext ctx, string hive, string keyPath, string category, bool isEnabled, List<AutorunEntry> list)
    {
        try
        {
            var valueNames = ctx.GetValueNames(hive, keyPath);
            foreach (var valName in valueNames)
            {
                object? valObj = ctx.GetValue(hive, keyPath, valName);
                if (valObj == null) continue;

                string cmd = valObj.ToString() ?? "";
                string resolvedPath = ExtractExecutablePath(cmd, ctx);
                bool fileExists = File.Exists(resolvedPath);

                var (isSuspicious, reason) = AnalyzeSuspicious(cmd, resolvedPath, fileExists);
                string? hash = fileExists ? TryComputeSha256(resolvedPath) : null;
                DateTime? lastMod = fileExists ? TryGetLastModified(resolvedPath) : null;

                list.Add(new AutorunEntry(
                    Guid.NewGuid().ToString("N"),
                    string.IsNullOrEmpty(valName) ? "(По умолчанию)" : valName,
                    cmd,
                    resolvedPath,
                    fileExists,
                    category,
                    hive,
                    keyPath,
                    valName,
                    isEnabled,
                    isSuspicious,
                    reason,
                    hash,
                    lastMod));
            }
        }
        catch
        {
            // не смогли прочитать ключ — идем дальше
        }
    }

    private void ScanWinlogon(ITargetContext ctx, List<AutorunEntry> list)
    {
        const string key = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
        string[] winlogonValues = ["Shell", "Userinit", "Taskman", "AppSetup", "VmApplet"];

        foreach (var vName in winlogonValues)
        {
            object? val = ctx.GetValue("HKLM", key, vName);
            if (val != null)
            {
                string cmd = val.ToString() ?? "";
                string resolved = ExtractExecutablePath(cmd, ctx);
                bool exists = File.Exists(resolved);

                bool isStandard = vName switch
                {
                    "Shell" => cmd.Trim().Equals("explorer.exe", StringComparison.OrdinalIgnoreCase),
                    "Userinit" => cmd.Contains("userinit.exe", StringComparison.OrdinalIgnoreCase),
                    _ => false
                };

                bool suspicious = !isStandard;
                string reason = suspicious ? $"Нестандартное значение в Winlogon\\{vName}" : "Стандартное значение Winlogon";

                list.Add(new AutorunEntry(
                    Guid.NewGuid().ToString("N"),
                    $"Winlogon: {vName}",
                    cmd,
                    resolved,
                    exists,
                    "Winlogon",
                    "HKLM",
                    key,
                    vName,
                    true,
                    suspicious,
                    reason));
            }

            // отключенные параметры Winlogon
            string disabledKey = key + "_ShiftUnlockerDisabled";
            object? disVal = ctx.GetValue("HKLM", disabledKey, vName);
            if (disVal != null)
            {
                string cmd = disVal.ToString() ?? "";
                string resolved = ExtractExecutablePath(cmd, ctx);
                bool exists = File.Exists(resolved);

                list.Add(new AutorunEntry(
                    Guid.NewGuid().ToString("N"),
                    $"Winlogon: {vName} (Отключено)",
                    cmd,
                    resolved,
                    exists,
                    "Winlogon (Отключено)",
                    "HKLM",
                    disabledKey,
                    vName,
                    false,
                    true,
                    "Отключенное пользователем значение Winlogon"));
            }
        }
    }

    private void ScanBootExecute(ITargetContext ctx, List<AutorunEntry> list)
    {
        const string key = @"SYSTEM\CurrentControlSet\Control\Session Manager";
        object? val = ctx.GetValue("HKLM", key, "BootExecute");
        if (val != null)
        {
            string cmd = val is string[] arr ? string.Join(" ", arr) : val.ToString() ?? "";
            bool isStandard = cmd.Trim().Equals("autocheck autochk *", StringComparison.OrdinalIgnoreCase);

            list.Add(new AutorunEntry(
                Guid.NewGuid().ToString("N"),
                "BootExecute",
                cmd,
                "",
                true,
                "Session Manager",
                "HKLM",
                key,
                "BootExecute",
                true,
                !isStandard,
                !isStandard ? "Модифицирован BootExecute (возможен руткит или сторонний чек)" : "Стандартный autocheck"));
        }
    }

    private void ScanIfeoDebuggers(ITargetContext ctx, List<AutorunEntry> list)
    {
        const string ifeoKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options";
        try
        {
            var subKeys = ctx.GetSubKeyNames("HKLM", ifeoKey);
            foreach (var app in subKeys)
            {
                string appKey = $"{ifeoKey}\\{app}";
                object? debugger = ctx.GetValue("HKLM", appKey, "Debugger");
                if (debugger != null)
                {
                    string dbgCmd = debugger.ToString() ?? "";
                    string resolved = ExtractExecutablePath(dbgCmd, ctx);
                    bool exists = File.Exists(resolved);

                    list.Add(new AutorunEntry(
                        Guid.NewGuid().ToString("N"),
                        $"IFEO Debugger: {app}",
                        dbgCmd,
                        resolved,
                        exists,
                        "IFEO Перехват",
                        "HKLM",
                        appKey,
                        "Debugger",
                        true,
                        true,
                        $"Перехват запуска программы {app} через ключ Debugger"));
                }
            }
        }
        catch
        {
            // ошибка чтения IFEO
        }
    }

    private void ScanStartupFolders(ITargetContext ctx, List<AutorunEntry> list)
    {
        // общая автозагрузка
        string commonStartup = Path.Combine(ctx.TargetRootPath, @"ProgramData\Microsoft\Windows\Start Menu\Programs\Startup");
        ScanFolderFiles(commonStartup, "Общая папка Автозагрузка", list, ctx);

        // автозагрузка по профилям
        foreach (var profile in ctx.UserProfiles)
        {
            string userStartup = Path.Combine(profile.ProfilePath, @"AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup");
            ScanFolderFiles(userStartup, $"Автозагрузка ({profile.UserName})", list, ctx);
        }
    }

    private void ScanFolderFiles(string folder, string category, List<AutorunEntry> list, ITargetContext ctx)
    {
        if (!Directory.Exists(folder)) return;

        try
        {
            foreach (var file in Directory.GetFiles(folder))
            {
                string fileName = Path.GetFileName(file);
                if (fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;

                bool isAlreadyDisabled = fileName.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
                string cleanName = isAlreadyDisabled ? fileName[..^9] : fileName;
                string cleanCategory = isAlreadyDisabled ? $"{category} (Отключено)" : category;

                var (suspicious, reason) = AnalyzeSuspicious(file, file, true);
                string? hash = TryComputeSha256(file);
                DateTime? lastMod = TryGetLastModified(file);

                list.Add(new AutorunEntry(
                    Guid.NewGuid().ToString("N"),
                    cleanName,
                    file,
                    file,
                    true,
                    cleanCategory,
                    "FS",
                    folder,
                    fileName,
                    !isAlreadyDisabled,
                    suspicious,
                    reason,
                    hash,
                    lastMod));
            }
        }
        catch
        {
            // нет доступа
        }
    }

    private void ScanScheduledTasks(ITargetContext ctx, List<AutorunEntry> list)
    {
        string tasksDir = Path.Combine(ctx.System32Directory, "Tasks");
        if (!Directory.Exists(tasksDir)) return;

        try
        {
            foreach (var taskFile in Directory.GetFiles(tasksDir, "*", SearchOption.AllDirectories))
            {
                try
                {
                    bool isAlreadyDisabled = taskFile.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
                    string xml = File.ReadAllText(taskFile);
                    var match = Regex.Match(xml, @"<Command>(.*?)</Command>", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        string cmd = match.Groups[1].Value.Trim();
                        string taskName = Path.GetFileName(taskFile);
                        if (isAlreadyDisabled) taskName = taskName[..^9];

                        string resolved = ExtractExecutablePath(cmd, ctx);
                        bool exists = File.Exists(resolved);

                        var (suspicious, reason) = AnalyzeSuspicious(cmd, resolved, exists);
                        if (suspicious || isAlreadyDisabled)
                        {
                            list.Add(new AutorunEntry(
                                Guid.NewGuid().ToString("N"),
                                $"Задача: {taskName}" + (isAlreadyDisabled ? " (Отключено)" : ""),
                                cmd,
                                resolved,
                                exists,
                                isAlreadyDisabled ? "Планировщик задач (Отключено)" : "Планировщик задач",
                                "FS_TASK",
                                taskFile,
                                taskName,
                                !isAlreadyDisabled,
                                true,
                                isAlreadyDisabled ? "Отключенная задача" : $"Подозрительная задача: {reason}"));
                        }
                    }
                }
                catch
                {
                    // битый xml пропускаем
                }
            }
        }
        catch
        {
            // ошибка обхода папки
        }
    }

    private void ScanComHijacks(ITargetContext ctx, List<AutorunEntry> list)
    {
        try
        {
            const string clsidRoot = @"Software\Classes\CLSID";
            foreach (var clsid in ctx.GetSubKeyNames("HKCU", clsidRoot).Take(300))
            {
                foreach (var srvType in new[] { "InprocServer32", "LocalServer32" })
                {
                    string subKey = $@"{clsidRoot}\{clsid}\{srvType}";
                    object? val = ctx.GetValue("HKCU", subKey, "");
                    if (val == null) continue;
                    string cmd = val.ToString() ?? "";
                    if (string.IsNullOrWhiteSpace(cmd)) continue;

                    string resolved = ExtractExecutablePath(cmd, ctx);
                    bool exists = File.Exists(resolved);
                    var (susp, reason) = AnalyzeSuspicious(cmd, resolved, exists);
                    if (!susp) continue;

                    list.Add(new AutorunEntry(
                        Guid.NewGuid().ToString("N"),
                        $"COM {clsid} ({srvType})",
                        cmd,
                        resolved,
                        exists,
                        "COM-Hijack (HKCU)",
                        "HKCU",
                        subKey,
                        "",
                        true,
                        true,
                        reason));
                }
            }
        }
        catch { }
    }

    private void ScanAppCompatShims(ITargetContext ctx, List<AutorunEntry> list)
    {
        try
        {
            const string customKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Custom";
            foreach (var exe in ctx.GetSubKeyNames("HKLM", customKey))
            {
                string sub = $@"{customKey}\{exe}";
                foreach (var valName in ctx.GetValueNames("HKLM", sub))
                {
                    list.Add(new AutorunEntry(
                        Guid.NewGuid().ToString("N"),
                        $"AppCompat Shim: {exe}",
                        valName,
                        valName,
                        File.Exists(valName),
                        "AppCompat Shims",
                        "HKLM",
                        sub,
                        valName,
                        true,
                        true,
                        "Нестандартный AppCompat Shim (.sdb)"));
                }
            }
        }
        catch { }
    }

    private void ScanTimeProviders(ITargetContext ctx, List<AutorunEntry> list)
    {
        try
        {
            const string tpRoot = @"SYSTEM\CurrentControlSet\Services\W32Time\TimeProviders";
            foreach (var prov in ctx.GetSubKeyNames("HKLM", tpRoot))
            {
                string sub = $@"{tpRoot}\{prov}";
                string dll = ctx.GetValue("HKLM", sub, "DllName")?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(dll)) continue;

                string resolved = ExtractExecutablePath(dll, ctx);
                bool exists = File.Exists(resolved);
                bool nonStd = !resolved.Contains(@"\System32\w32time.dll", StringComparison.OrdinalIgnoreCase) &&
                              !resolved.Contains(@"\System32\vmictimeprovider.dll", StringComparison.OrdinalIgnoreCase);
                if (!nonStd) continue;

                list.Add(new AutorunEntry(
                    Guid.NewGuid().ToString("N"),
                    $"TimeProvider: {prov}",
                    dll,
                    resolved,
                    exists,
                    "Time Providers",
                    "HKLM",
                    sub,
                    "DllName",
                    true,
                    true,
                    "Нестандартный TimeProvider DLL"));
            }
        }
        catch { }
    }

    private void ScanPrintMonitors(ITargetContext ctx, List<AutorunEntry> list)
    {
        try
        {
            const string pmRoot = @"SYSTEM\CurrentControlSet\Control\Print\Monitors";
            foreach (var mon in ctx.GetSubKeyNames("HKLM", pmRoot))
            {
                string sub = $@"{pmRoot}\{mon}";
                string driver = ctx.GetValue("HKLM", sub, "Driver")?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(driver)) continue;

                string resolved = Path.IsPathRooted(driver) ? driver : Path.Combine(ctx.System32Directory, driver);
                bool exists = File.Exists(resolved);
                var (susp, reason) = AnalyzeSuspicious(driver, resolved, exists);
                if (!susp) continue;

                list.Add(new AutorunEntry(
                    Guid.NewGuid().ToString("N"),
                    $"PrintMonitor: {mon}",
                    driver,
                    resolved,
                    exists,
                    "Print Monitors",
                    "HKLM",
                    sub,
                    "Driver",
                    true,
                    true,
                    reason));
            }
        }
        catch { }
    }

    private void ScanWinsockLsp(ITargetContext ctx, List<AutorunEntry> list)
    {
        try
        {
            const string lspRoot = @"SYSTEM\CurrentControlSet\Services\WinSock2\Parameters\Protocol_Catalog9\Catalog_Entries";
            foreach (var entry in ctx.GetSubKeyNames("HKLM", lspRoot))
            {
                string sub = $@"{lspRoot}\{entry}";
                if (ctx.GetValue("HKLM", sub, "PackedCatalogItem") is byte[] bytes && bytes.Length > 10)
                {
                    // Первые 260 байт содержат ANSI-путь к DLL провайдера
                    int end = Array.IndexOf(bytes, (byte)0);
                    string rawPath = end > 0 ? System.Text.Encoding.ASCII.GetString(bytes, 0, Math.Min(end, 260)) : "";
                    if (!string.IsNullOrWhiteSpace(rawPath))
                    {
                        string resolved = ExtractExecutablePath(rawPath, ctx);
                        bool exists = File.Exists(resolved);
                        var (susp, reason) = AnalyzeSuspicious(rawPath, resolved, exists);
                        if (susp)
                        {
                            list.Add(new AutorunEntry(
                                Guid.NewGuid().ToString("N"),
                                $"Winsock LSP: {entry}",
                                rawPath,
                                resolved,
                                exists,
                                "Winsock LSP",
                                "HKLM",
                                sub,
                                "PackedCatalogItem",
                                true,
                                true,
                                reason));
                        }
                    }
                }
            }
        }
        catch { }
    }

    private void ScanPowerShellProfiles(ITargetContext ctx, List<AutorunEntry> list)
    {
        try
        {
            var candidates = new List<string>
            {
                Path.Combine(ctx.System32Directory, @"WindowsPowerShell\v1.0\profile.ps1"),
                Path.Combine(ctx.System32Directory, @"WindowsPowerShell\v1.0\Microsoft.PowerShell_profile.ps1")
            };

            string usersDir = Path.Combine(ctx.TargetRootPath, "Users");
            if (Directory.Exists(usersDir))
            {
                foreach (var uDir in Directory.GetDirectories(usersDir))
                {
                    candidates.Add(Path.Combine(uDir, @"Documents\WindowsPowerShell\Microsoft.PowerShell_profile.ps1"));
                    candidates.Add(Path.Combine(uDir, @"Documents\PowerShell\Microsoft.PowerShell_profile.ps1"));
                }
            }

            foreach (var psPath in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!File.Exists(psPath)) continue;
                list.Add(new AutorunEntry(
                    Guid.NewGuid().ToString("N"),
                    $"PS Profile: {Path.GetFileName(psPath)}",
                    psPath,
                    psPath,
                    true,
                    "PowerShell Профили",
                    "FS_STARTUP",
                    psPath,
                    Path.GetFileName(psPath),
                    true,
                    true,
                    "Обнаружен скрипт автозапуска PowerShell-профиля"));
            }
        }
        catch { }
    }

    private void ScanSuspiciousEnvVars(ITargetContext ctx, List<AutorunEntry> list)
    {
        string[] envNames = ["COR_PROFILER", "COR_PROFILER_PATH", "COR_ENABLE_PROFILING", "__COMPAT_LAYER"];
        var scopes = new (string Hive, string Key)[]
        {
            ("HKCU", @"Environment"),
            ("HKLM", @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment")
        };

        foreach (var (hive, key) in scopes)
        {
            foreach (var env in envNames)
            {
                try
                {
                    object? val = ctx.GetValue(hive, key, env);
                    if (val == null || string.IsNullOrWhiteSpace(val.ToString())) continue;
                    string sVal = val.ToString()!;
                    list.Add(new AutorunEntry(
                        Guid.NewGuid().ToString("N"),
                        $"ENV: {env}",
                        sVal,
                        sVal,
                        File.Exists(sVal),
                        "Переменные среды",
                        hive,
                        key,
                        env,
                        true,
                        true,
                        $"Потенциально опасная переменная среды ({env})"));
                }
                catch { }
            }
        }
    }

    public static (bool Suspicious, string Reason) AnalyzeSuspicious(string command, string resolvedPath, bool fileExists)
    {
        string lowerCmd = command.ToLowerInvariant();

        // скриптовые движки и обфускация
        if (lowerCmd.Contains("powershell") && (lowerCmd.Contains("-enc") || lowerCmd.Contains("-e ") || lowerCmd.Contains("downloadstring")))
        {
            return (true, "Запуск PowerShell со скрытыми или закодированными командами (-enc)");
        }

        if (lowerCmd.Contains("mshta") || lowerCmd.Contains("wscript") || lowerCmd.Contains("cscript") || lowerCmd.Contains("regsvr32 /s /u /i:"))
        {
            return (true, "Использование скриптового интерпретатора (mshta / wscript / regsvr32) в автозагрузке");
        }

        if (lowerCmd.Contains("cmd.exe /c") || lowerCmd.Contains("cmd /c"))
        {
            return (true, "Запуск консольной команды через cmd /c");
        }

        // подозрительные пути (temp, appdata и т.д.)
        if (!string.IsNullOrEmpty(resolvedPath))
        {
            string lowerPath = resolvedPath.ToLowerInvariant();
            if (lowerPath.Contains(@"\appdata\local\temp") || lowerPath.Contains(@"\windows\temp"))
            {
                return (true, "Файл запускается из папки временных файлов (Temp)");
            }

            if (lowerPath.Contains(@"\appdata\roaming") || lowerPath.Contains(@"\appdata\local"))
            {
                string fileName = Path.GetFileName(resolvedPath);
                if (lowerPath.EndsWith(@"\" + fileName.ToLowerInvariant()) && Path.GetDirectoryName(resolvedPath)?.EndsWith("AppData", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return (true, "Файл исполняется напрямую из корня AppData");
                }
            }

            if (lowerPath.Contains("$recycle.bin"))
            {
                return (true, "Файл запускается из корзины ($Recycle.Bin)");
            }

            // двойные расширения
            string fileNameOnly = Path.GetFileName(resolvedPath);
            if (Regex.IsMatch(fileNameOnly, @"\.(docx|pdf|txt|xlsx|jpg|png)\.(exe|scr|bat|cmd|vbs|js)$", RegexOptions.IgnoreCase))
            {
                return (true, "Опасное двойное расширение (маскировка под документ)");
            }

            // файла нет на диске
            if (!fileExists && !string.IsNullOrWhiteSpace(resolvedPath))
            {
                return (true, "Указанный файл автозапуска отсутствует на диске");
            }
        }

        return (false, "Обычная запись");
    }

    public static string ExtractExecutablePath(string rawCommand, ITargetContext ctx)
    {
        if (string.IsNullOrWhiteSpace(rawCommand)) return "";

        string trimmed = rawCommand.Trim();
        string candidate = trimmed;

        if (trimmed.StartsWith('\"'))
        {
            int nextQuote = trimmed.IndexOf('\"', 1);
            if (nextQuote > 1)
            {
                candidate = trimmed[1..nextQuote];
            }
        }
        else
        {
            int spaceIdx = trimmed.IndexOf(' ');
            if (spaceIdx > 0)
            {
                candidate = trimmed[..spaceIdx];
            }
        }

        return ctx.MapPath(candidate);
    }

    public Result DisableEntry(ITargetContext ctx, BackupManager bm, AutorunEntry entry)
    {
        try
        {
            // в Winlogon не удаляем ключ, а возвращаем дефолт
            if (entry.LocationType.StartsWith("Winlogon", StringComparison.OrdinalIgnoreCase))
            {
                string disabledKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon_ShiftUnlockerDisabled";
                bm.BackupAndSetRegistryValue(ctx, "HKLM", disabledKey, entry.ValueName, entry.Command, RegistryValueKind.String, $"Backup disabled Winlogon value: {entry.ValueName}");

                if (entry.ValueName.Equals("Shell", StringComparison.OrdinalIgnoreCase))
                {
                    return bm.BackupAndSetRegistryValue(ctx, "HKLM", entry.KeyOrDirectoryPath, "Shell", "explorer.exe", RegistryValueKind.String, "Reset Winlogon Shell to default (explorer.exe)");
                }
                if (entry.ValueName.Equals("Userinit", StringComparison.OrdinalIgnoreCase))
                {
                    string standardUserinit = ctx.MapPath(@"C:\Windows\System32\userinit.exe,");
                    return bm.BackupAndSetRegistryValue(ctx, "HKLM", entry.KeyOrDirectoryPath, "Userinit", standardUserinit, RegistryValueKind.String, "Reset Winlogon Userinit to default");
                }
                return bm.BackupAndDeleteRegistryValue(ctx, "HKLM", entry.KeyOrDirectoryPath, entry.ValueName, $"Removed non-standard Winlogon entry: {entry.ValueName}");
            }

            // обычные ключи Run / IFEO
            if (entry.Hive is "HKLM" or "HKCU")
            {
                string disabledKey = entry.KeyOrDirectoryPath + "_ShiftUnlockerDisabled";
                var r1 = bm.BackupAndSetRegistryValue(ctx, entry.Hive, disabledKey, entry.ValueName, entry.Command, RegistryValueKind.String, $"Disabled autorun entry: {entry.Name}");
                var r2 = bm.BackupAndDeleteRegistryValue(ctx, entry.Hive, entry.KeyOrDirectoryPath, entry.ValueName, $"Removed active autorun: {entry.Name}");
                return r1.Success && r2.Success ? Result.Ok() : Result.Fail(r1.Error ?? r2.Error ?? "Failed to disable autorun entry in registry.");
            }

            // файл из папки автозагрузки
            if (entry.Hive == "FS" && File.Exists(entry.ResolvedFilePath))
            {
                string src = entry.ResolvedFilePath;
                string dst = src.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase) ? src : src + ".disabled";
                if (src == dst) return Result.Ok();

                File.SetAttributes(src, FileAttributes.Normal);
                File.Move(src, dst, true);
                bm.RegisterUndo($"Enable startup file {entry.Name}", () =>
                {
                    if (File.Exists(dst))
                    {
                        File.SetAttributes(dst, FileAttributes.Normal);
                        File.Move(dst, src, true);
                    }
                    return Result.Ok();
                });
                bm.Log("Autorun", $"Disabled startup file: {entry.Name} -> {Path.GetFileName(dst)}");
                return Result.Ok();
            }

            // файл задачи планировщика
            if (entry.Hive == "FS_TASK" && File.Exists(entry.KeyOrDirectoryPath))
            {
                string src = entry.KeyOrDirectoryPath;
                string dst = src.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase) ? src : src + ".disabled";
                if (src == dst) return Result.Ok();

                File.SetAttributes(src, FileAttributes.Normal);
                File.Move(src, dst, true);
                bm.RegisterUndo($"Enable scheduled task {entry.Name}", () =>
                {
                    if (File.Exists(dst))
                    {
                        File.SetAttributes(dst, FileAttributes.Normal);
                        File.Move(dst, src, true);
                    }
                    return Result.Ok();
                });
                bm.Log("Autorun", $"Disabled scheduled task: {entry.Name}");
                return Result.Ok();
            }

            return Result.Fail("Тип расположения записи не поддерживает отключение.");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result.Fail($"Отказано в доступе (требуются права Администратора): {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    public Result EnableEntry(ITargetContext ctx, BackupManager bm, AutorunEntry entry)
    {
        try
        {
            // отключенный Winlogon
            if (entry.LocationType.Contains("Winlogon") && entry.KeyOrDirectoryPath.EndsWith("_ShiftUnlockerDisabled"))
            {
                string originalKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
                var r1 = bm.BackupAndSetRegistryValue(ctx, "HKLM", originalKey, entry.ValueName, entry.Command, RegistryValueKind.String, $"Restored Winlogon value: {entry.ValueName}");
                var r2 = bm.BackupAndDeleteRegistryValue(ctx, "HKLM", entry.KeyOrDirectoryPath, entry.ValueName, $"Removed disabled Winlogon backup: {entry.ValueName}");
                return r1.Success && r2.Success ? Result.Ok() : Result.Fail(r1.Error ?? r2.Error ?? "Failed to enable Winlogon entry.");
            }

            // обычные ключи Run
            if (entry.Hive is "HKLM" or "HKCU" && entry.KeyOrDirectoryPath.EndsWith("_ShiftUnlockerDisabled"))
            {
                string originalKey = entry.KeyOrDirectoryPath.Replace("_ShiftUnlockerDisabled", "");
                var r1 = bm.BackupAndSetRegistryValue(ctx, entry.Hive, originalKey, entry.ValueName, entry.Command, RegistryValueKind.String, $"Re-enabled autorun: {entry.Name}");
                var r2 = bm.BackupAndDeleteRegistryValue(ctx, entry.Hive, entry.KeyOrDirectoryPath, entry.ValueName, $"Removed disabled entry: {entry.Name}");
                return r1.Success && r2.Success ? Result.Ok() : Result.Fail(r1.Error ?? r2.Error ?? "Failed to re-enable autorun entry in registry.");
            }

            // файл из папки автозагрузки
            if (entry.Hive == "FS")
            {
                string currentPath = entry.ResolvedFilePath;
                if (!File.Exists(currentPath) && File.Exists(currentPath + ".disabled"))
                {
                    currentPath += ".disabled";
                }

                if (File.Exists(currentPath) && currentPath.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
                {
                    string targetPath = currentPath[..^9]; // Strip .disabled
                    File.SetAttributes(currentPath, FileAttributes.Normal);
                    File.Move(currentPath, targetPath, true);
                    bm.RegisterUndo($"Disable startup file {entry.Name}", () =>
                    {
                        if (File.Exists(targetPath))
                        {
                            File.SetAttributes(targetPath, FileAttributes.Normal);
                            File.Move(targetPath, currentPath, true);
                        }
                        return Result.Ok();
                    });
                    bm.Log("Autorun", $"Re-enabled startup file: {entry.Name}");
                    return Result.Ok();
                }
            }

            // файл задачи планировщика
            if (entry.Hive == "FS_TASK")
            {
                string currentPath = entry.KeyOrDirectoryPath;
                if (!File.Exists(currentPath) && File.Exists(currentPath + ".disabled"))
                {
                    currentPath += ".disabled";
                }

                if (File.Exists(currentPath) && currentPath.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
                {
                    string targetPath = currentPath[..^9];
                    File.SetAttributes(currentPath, FileAttributes.Normal);
                    File.Move(currentPath, targetPath, true);
                    bm.RegisterUndo($"Disable scheduled task {entry.Name}", () =>
                    {
                        if (File.Exists(targetPath))
                        {
                            File.SetAttributes(targetPath, FileAttributes.Normal);
                            File.Move(targetPath, currentPath, true);
                        }
                        return Result.Ok();
                    });
                    bm.Log("Autorun", $"Re-enabled scheduled task: {entry.Name}");
                    return Result.Ok();
                }
            }

            return Result.Fail("Запись не находится в состоянии 'Отключено'.");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result.Fail($"Отказано в доступе (требуются права Администратора): {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    public Result DeleteEntry(ITargetContext ctx, BackupManager bm, AutorunEntry entry)
    {
        try
        {
            // сбрасываем Winlogon на стандарт вместо удаления
            if (entry.LocationType.StartsWith("Winlogon", StringComparison.OrdinalIgnoreCase))
            {
                if (entry.ValueName.Equals("Shell", StringComparison.OrdinalIgnoreCase))
                {
                    return bm.BackupAndSetRegistryValue(ctx, "HKLM", entry.KeyOrDirectoryPath, "Shell", "explorer.exe", RegistryValueKind.String, "Reset Winlogon Shell to default (explorer.exe)");
                }
                if (entry.ValueName.Equals("Userinit", StringComparison.OrdinalIgnoreCase))
                {
                    string standardUserinit = ctx.MapPath(@"C:\Windows\System32\userinit.exe,");
                    return bm.BackupAndSetRegistryValue(ctx, "HKLM", entry.KeyOrDirectoryPath, "Userinit", standardUserinit, RegistryValueKind.String, "Reset Winlogon Userinit to default");
                }
                return bm.BackupAndDeleteRegistryValue(ctx, "HKLM", entry.KeyOrDirectoryPath, entry.ValueName, $"Deleted Winlogon entry: {entry.ValueName}");
            }

            // обычные ключи Run / IFEO
            if (entry.Hive is "HKLM" or "HKCU")
            {
                return bm.BackupAndDeleteRegistryValue(ctx, entry.Hive, entry.KeyOrDirectoryPath, entry.ValueName, $"Deleted autorun: {entry.Name}");
            }

            // файл из папки автозагрузки
            if (entry.Hive == "FS" && File.Exists(entry.ResolvedFilePath))
            {
                bm.BackupFile(entry.ResolvedFilePath);
                File.SetAttributes(entry.ResolvedFilePath, FileAttributes.Normal);
                File.Delete(entry.ResolvedFilePath);
                bm.Log("Autorun", $"Deleted startup file: {entry.ResolvedFilePath}");
                return Result.Ok();
            }

            // файл задачи планировщика
            if (entry.Hive == "FS_TASK" && File.Exists(entry.KeyOrDirectoryPath))
            {
                bm.BackupFile(entry.KeyOrDirectoryPath);
                File.SetAttributes(entry.KeyOrDirectoryPath, FileAttributes.Normal);
                File.Delete(entry.KeyOrDirectoryPath);
                bm.Log("Autorun", $"Deleted scheduled task: {entry.KeyOrDirectoryPath}");
                return Result.Ok();
            }

            return Result.Fail("Неподдерживаемое расположение для удаления записи.");
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result.Fail($"Отказано в доступе (требуются права Администратора): {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    private static string? TryComputeSha256(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var fs = File.OpenRead(path);
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }

    private static DateTime? TryGetLastModified(string path)
    {
        try
        {
            return File.GetLastWriteTime(path);
        }
        catch
        {
            return null;
        }
    }
}
