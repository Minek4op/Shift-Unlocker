using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using ShiftUnlocker.Core.Common;
using ShiftUnlocker.Core.Security;

namespace ShiftUnlocker.Core.Modules.Launcher;

public class LauncherService
{
    private readonly string _configFilePath;
    private readonly string _toolsDirectory;
    private readonly Assembly? _resourceAssembly;

    public string ToolsDirectory => _toolsDirectory;

    public LauncherService(string? baseDir = null, Assembly? resourceAssembly = null)
    {
        string root = baseDir ?? AppDomain.CurrentDomain.BaseDirectory;
        _configFilePath = Path.Combine(root, "utilities.json");
        _toolsDirectory = Path.Combine(root, "Tools");
        _resourceAssembly = resourceAssembly ?? Assembly.GetEntryAssembly();

        try
        {
            if (!Directory.Exists(_toolsDirectory))
            {
                Directory.CreateDirectory(_toolsDirectory);
            }
        }
        catch { }
    }

    public List<UtilityItem> GetAllUtilities()
    {
        var list = new List<UtilityItem>();

        // вшитые сторонние утилиты
        list.AddRange(GetEmbeddedUtilities());

        // системные утилиты Windows
        list.AddRange(GetBuiltInSystemUtilities());

        // пользовательские из utilities.json
        var customItems = LoadConfigUtilities();

        // поиск в папке Tools
        var discoveredItems = ScanForKnownUtilities();

        // объединяем списки без дублей
        var merged = new Dictionary<string, UtilityItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in customItems.Concat(discoveredItems))
        {
            merged[item.ExecutablePath] = item;
        }

        list.AddRange(merged.Values);
        return list;
    }

    public List<UtilityItem> GetEmbeddedUtilities()
    {
        var list = new List<UtilityItem>();
        if (_resourceAssembly == null) return list;

        const string marker = ".EmbeddedTools.";
        foreach (var resName in _resourceAssembly.GetManifestResourceNames())
        {
            int idx = resName.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;

            string fileName = resName.Substring(idx + marker.Length);
            if (!fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;

            var (friendlyName, cat, desc) = DescribeEmbeddedTool(fileName);
            list.Add(new UtilityItem(
                "emb_" + fileName.ToLowerInvariant(),
                friendlyName,
                cat,
                desc,
                $"[Встроено] {fileName}",
                "",
                true,
                true,
                true,
                true,
                false,
                resName));
        }

        return list;
    }

    private static (string Name, string Cat, string Desc) DescribeEmbeddedTool(string fileName)
    {
        return fileName.ToLowerInvariant() switch
        {
            "su.exe" => ("Simple Unlocker", "Разблокировка", "Встроенная утилита Simple Unlocker"),
            "nhelperv4.exe" => ("NHelper v4", "Восстановление", "Встроенный комплекс восстановления NHelper v4"),
            "recoverykit.exe" => ("RecoveryKit", "Восстановление", "Встроенный набор утилит RecoveryKit"),
            _ => (Path.GetFileNameWithoutExtension(fileName), "Встроенные", $"Встроенная утилита {fileName}")
        };
    }

    private List<UtilityItem> GetBuiltInSystemUtilities()
    {
        string sys = Environment.GetFolderPath(Environment.SpecialFolder.System);
        string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var defs = new (string Name, string Exe, string Desc, string Cat)[]
        {
            ("Проводник Windows", "explorer.exe", "Штатный проводник Windows", "Система"),
            ("Командная строка", "cmd.exe", "Интерпретатор команд Windows", "Система"),
            ("PowerShell", "powershell.exe", "Оболочка автоматизации PowerShell", "Система"),
            ("Редактор реестра", "regedit.exe", "Штатный редактор реестра", "Система"),
            ("Диспетчер задач", "taskmgr.exe", "Штатный диспетчер задач", "Система"),
            ("Блокнот", "notepad.exe", "Простой текстовый редактор", "Система"),
            ("DiskPart", "diskpart.exe", "Консольная утилита управления дисками", "Диски"),
            ("Управление дисками", "diskmgmt.msc", "Оснастка управления томами и дисками", "Диски"),
            ("Диспетчер устройств", "devmgmt.msc", "Просмотр и управление устройствами и драйверами", "Система"),
            ("Сведения о системе", "msinfo32.exe", "Полная информация о конфигурации ПК", "Диагностика")
        };

        var list = new List<UtilityItem>();
        foreach (var d in defs)
        {
            string path = Path.Combine(sys, d.Exe);
            if (!File.Exists(path) && File.Exists(Path.Combine(win, d.Exe)))
            {
                path = Path.Combine(win, d.Exe);
            }
            bool exists = File.Exists(path);
            list.Add(new UtilityItem(
                d.Exe,
                d.Name,
                d.Cat,
                d.Desc,
                path,
                "",
                true,
                false,
                exists,
                true));
        }

        return list;
    }

    private List<UtilityItem> LoadConfigUtilities()
    {
        var list = new List<UtilityItem>();
        if (!File.Exists(_configFilePath)) return list;

        try
        {
            string json = File.ReadAllText(_configFilePath);
            var items = JsonSerializer.Deserialize<List<UtilityItem>>(json);
            if (items != null)
            {
                foreach (var it in items)
                {
                    bool exists = File.Exists(it.ExecutablePath);
                    list.Add(it with { ExistsOnDisk = exists, IsBuiltIn = false, IsUserAdded = true });
                }
            }
        }
        catch { }

        return list;
    }

    public void SaveConfigUtilities(IEnumerable<UtilityItem> items)
    {
        try
        {
            var userAdded = items.Where(i => i.IsUserAdded).ToList();
            string json = JsonSerializer.Serialize(userAdded, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configFilePath, json);
        }
        catch { }
    }

    public Result RemoveCustomUtility(string idOrPath)
    {
        try
        {
            var custom = LoadConfigUtilities();
            int removed = custom.RemoveAll(u =>
                string.Equals(u.Id, idOrPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(u.ExecutablePath, idOrPath, StringComparison.OrdinalIgnoreCase));

            if (removed == 0) return Result.Fail("Пользовательская утилита не найдена.");

            SaveConfigUtilities(custom);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail($"Ошибка удаления утилиты: {ex.Message}");
        }
    }

    private List<UtilityItem> ScanForKnownUtilities()
    {
        var list = new List<UtilityItem>();
        var knownNames = new (string Pattern, string Name, string Cat, string Desc)[]
        {
            ("autoruns*.exe", "Sysinternals Autoruns", "Автозагрузка", "Глубокий анализ всех автозапусков"),
            ("procexp*.exe", "Sysinternals Process Explorer", "Процессы", "Мощный диспетчер процессов с дескрипторами"),
            ("procmon*.exe", "Sysinternals Process Monitor", "Мониторинг", "Мониторинг обращений к реестру и файлам"),
            ("tcpview*.exe", "Sysinternals TCPView", "Сеть", "Активные соединения и открытые порты"),
            ("processhacker.exe", "Process Hacker", "Процессы", "Расширенный диспетчер процессов"),
            ("SystemInformer.exe", "System Informer", "Процессы", "Преемник Process Hacker"),
            ("tdsskiller.exe", "Kaspersky TDSSKiller", "Антируткит", "Поиск руткитов и буткитов"),
            ("AdwCleaner.exe", "Malwarebytes AdwCleaner", "Антивирус", "Удаление рекламного ПО и хиджакеров"),
            ("mbam.exe", "Malwarebytes Anti-Malware", "Антивирус", "Сканер вредоносных программ"),
            ("cureit.exe", "Dr.Web CureIt!", "Антивирус", "Автономный лечащий сканер"),
            ("kvrt.exe", "Kaspersky Virus Removal Tool", "Антивирус", "Бесплатный сканер Касперского"),
            ("HitmanPro*.exe", "HitmanPro", "Антивирус", "Облачный сканер вредоносного ПО"),
            ("RogueKiller*.exe", "RogueKiller", "Антивирус", "Поиск вредоносных процессов и драйверов"),
            ("Explorer++.exe", "Explorer++", "Файлы", "Легковесный файловый менеджер"),
            ("7zFM.exe", "7-Zip File Manager", "Файлы", "Архиватор и просмотр файлов"),
            ("notepad++.exe", "Notepad++", "Текст", "Продвинутый текстовый редактор"),
            ("DiskInfo*.exe", "CrystalDiskInfo", "Диски", "Диагностика SMART накопителей")
        };

        var searchDirs = new List<string>();
        if (Directory.Exists(_toolsDirectory))
        {
            searchDirs.Add(_toolsDirectory);
        }

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.IsReady && drive.DriveType is DriveType.Removable or DriveType.Fixed)
            {
                string driveTools = Path.Combine(drive.RootDirectory.FullName, "Tools");
                if (Directory.Exists(driveTools))
                {
                    searchDirs.Add(driveTools);
                }
            }
        }

        foreach (var dir in searchDirs)
        {
            foreach (var known in knownNames)
            {
                try
                {
                    var foundFiles = Directory.GetFiles(dir, known.Pattern, SearchOption.AllDirectories);
                    foreach (var file in foundFiles)
                    {
                        list.Add(new UtilityItem(
                            Guid.NewGuid().ToString("N"),
                            known.Name,
                            known.Cat,
                            known.Desc,
                            file,
                            "",
                            true,
                            true,
                            true,
                            false));
                    }
                }
                catch { }
            }
        }

        return list;
    }

    public Result LaunchUtility(UtilityItem item, bool forceRandomName = false)
    {
        string pathToRun = item.ExecutablePath;
        bool useRandom = forceRandomName || item.RunRandomName;

        if (!string.IsNullOrEmpty(item.EmbeddedResourceName))
        {
            if (_resourceAssembly == null)
            {
                return Result.Fail("Не найдена сборка со встроенными ресурсами.");
            }

            using var stream = _resourceAssembly.GetManifestResourceStream(item.EmbeddedResourceName);
            if (stream == null)
            {
                return Result.Fail($"Встроенный ресурс не найден: {item.EmbeddedResourceName}");
            }

            try
            {
                string fileName = useRandom
                    ? RandomLauncher.GenerateRandomName(10) + ".exe"
                    : item.ExecutablePath.Replace("[Встроено] ", "").Trim();

                string extractDir = useRandom
                    ? Path.GetTempPath()
                    : Path.Combine(Path.GetTempPath(), "SU_EmbeddedTools");

                if (!Directory.Exists(extractDir)) Directory.CreateDirectory(extractDir);

                string tempTarget = Path.Combine(extractDir, fileName);
                using (var fs = new FileStream(tempTarget, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.CopyTo(fs);
                }
                pathToRun = tempTarget;
            }
            catch (Exception ex)
            {
                return Result.Fail($"Не удалось распаковать встроенную утилиту: {ex.Message}");
            }
        }
        else
        {
            if (!File.Exists(item.ExecutablePath))
            {
                return Result.Fail($"Файл утилиты не найден: {item.ExecutablePath}");
            }

            if (useRandom)
            {
                string randName = RandomLauncher.GenerateRandomName(10) + Path.GetExtension(item.ExecutablePath);
                string tempTarget = Path.Combine(Path.GetTempPath(), randName);
                try
                {
                    File.Copy(item.ExecutablePath, tempTarget, true);
                    pathToRun = tempTarget;
                }
                catch
                {
                    // если скопировать не вышло — запускаем как есть
                    pathToRun = item.ExecutablePath;
                }
            }
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = pathToRun,
                Arguments = item.Arguments,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(pathToRun) ?? ""
            };

            if (item.RunAsAdmin)
            {
                psi.Verb = "runas";
            }

            Process.Start(psi);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail($"Ошибка запуска утилиты: {ex.Message}");
        }
    }
}
