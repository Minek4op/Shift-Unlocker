namespace ShiftUnlocker.App.UI;

public enum AppLanguage
{
    Ru,
    En
}

public static class LangManager
{
    public static AppLanguage CurrentLanguage { get; set; } = AppLanguage.Ru;
    public static event Action? LanguageChanged;

    private static readonly Dictionary<string, (string Ru, string En)> Strings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["menu_grp_diag"] = ("ДИАГНОСТИКА", "DIAGNOSTICS"),
        ["menu_grp_heal"] = ("ЛЕЧЕНИЕ", "RECOVERY"),
        ["menu_grp_sys"] = ("СИСТЕМА", "SYSTEM"),
        ["menu_grp_disk"] = ("ДИСКИ И ЗАГРУЗКА", "DISKS & BOOT"),
        ["menu_grp_tools"] = ("ИНСТРУМЕНТЫ", "TOOLS"),

        ["nav_scan"] = ("[Alt+1]  Обзор / Скан", "[Alt+1]  Overview / Scan"),
        ["nav_autorun"] = ("[Alt+2]  Автозагрузка", "[Alt+2]  Autoruns"),
        ["nav_proc"] = ("[Alt+3]  Диспетчер задач", "[Alt+3]  Task Manager"),
        ["nav_mbr"] = ("[Alt+4]  MBR / Загрузчик", "[Alt+4]  MBR / Boot"),
        ["nav_restrict"] = ("[Alt+5]  Разблокировка", "[Alt+5]  Unlocker"),
        ["nav_tools"] = ("[Alt+6]  Утилиты", "[Alt+6]  Utilities"),
        ["nav_reg"] = ("[Alt+7]  Редактор реестра", "[Alt+7]  Registry Editor"),
        ["nav_files"] = ("[Alt+8]  Проводник", "[Alt+8]  File Explorer"),
        ["nav_wizard"] = ("[Alt+9]  Мастер и Журнал", "[Alt+9]  Wizard & Log"),

        ["btn_target"] = ("Выбор целевой ОС", "Target OS"),
        ["btn_undo"] = ("Откатить (Undo)", "Undo"),
        ["btn_elevate"] = ("Права Администратора", "Run as Admin"),
        ["btn_palette"] = ("Команды (Ctrl+K)", "Commands (Ctrl+K)"),
        ["btn_help"] = ("Справка (F1)", "Help (F1)"),

        ["what_happened"] = ("Что случилось? Выберите сценарий:", "What happened? Choose a scenario:"),
        ["sc_tools_blocked"] = ("Не открывается диспетчер задач / реестр / cmd", "TaskMgr / Regedit / CMD won't open"),
        ["sc_usb_shortcuts"] = ("Вместо папок ярлыки на флешке", "USB folders turned into shortcuts"),
        ["sc_banner"] = ("Экран-блокировщик с текстом / баннером", "Lockscreen banner / ransom message"),
        ["sc_encrypted"] = ("Файлы зашифрованы", "Files are encrypted"),
        ["sc_noboot"] = ("Компьютер не загружается", "PC fails to boot"),
        ["sc_fullcheck"] = ("Просто проверить систему", "Just scan the system"),

        ["empty_hint"] = ("Откройте вкладку ещё раз или нажмите Обновить, если автосканирование не запустилось.", "Re-open the tab or click Refresh if auto-scan did not start."),
        ["scanning_msg"] = ("Идёт сканирование…", "Scanning…"),

        ["queue_apply"] = ("Применить всё", "Apply All"),
        ["queue_clear"] = ("Очистить", "Clear"),
        ["queue_title"] = ("К применению", "Pending changes")
    };

    private static readonly Dictionary<int, (string TitleRu, string BodyRu, string TitleEn, string BodyEn)> HelpTopics = new()
    {
        [0] = (
            "Обзор и быстрое сканирование (Alt+1)",
            "Показывает быстрые сценарии диагностики («Что случилось?») и проверяет ключевые точки заражения: перехваты IFEO Debugger, подмену Winlogon Shell/Userinit, ассоциации .exe, прокси и политики.\n\nВыберите найденные проблемы и добавьте их в очередь исправлений или нажмите «Исправить всё».",
            "Overview & Quick Scan (Alt+1)",
            "Provides quick diagnostic scenarios ('What happened?') and scans key infection points: IFEO Debugger hijacks, Winlogon Shell/Userinit, .exe associations, proxy settings, and system policies.\n\nSelect findings to queue fixes or click 'Fix All'."
        ),
        [1] = (
            "Автозагрузка (Alt+2)",
            "Сканирует Run/RunOnce, Winlogon, BootExecute, IFEO, папки автозагрузки и задачи планировщика. Подозрительные записи подсвечиваются эвристикой.\n\nВы можете отключить, удалить или поместить файл в карантин через очередь изменений.",
            "Autoruns (Alt+2)",
            "Scans Run/RunOnce, Winlogon, BootExecute, IFEO, Startup folders, and Scheduled Tasks. Suspicious entries are flagged by heuristics.\n\nDisable, delete, or quarantine entries safely with BackupManager undo support."
        ),
        [2] = (
            "Диспетчер задач, службы и сеть (Alt+3)",
            "Позволяет завершать, замораживать (Suspend) и возобновлять процессы, включать Watchdog-киллер по имени, управлять типом запуска служб и просматривать активные сетевые соединения.",
            "Task Manager, Services & Network (Alt+3)",
            "Terminate, suspend, or resume processes, run name-based Watchdog killer, configure Windows service startup modes, and inspect active TCP/UDP connections."
        ),
        [3] = (
            "MBR / Загрузчик и Диски (Alt+4)",
            "Читает 0-й сектор диска (512 байт MBR), ищет аномалии буткитов, позволяет делать бэкап и восстановление MBR, запускать bootsect, bcdboot, офлайн SFC/DISM и управлять разделами.",
            "MBR / Boot & Disks (Alt+4)",
            "Reads sector 0 (512-byte MBR), detects bootkit anomalies, backs up/restores MBR, runs bootsect, bcdboot, offline SFC/DISM, and manages volumes via DiskPart."
        ),
        [4] = (
            "Разблокировка и восстановление (Alt+5)",
            "Снимает и устанавливает политики Windows, позволяет добавлять свои правила реестра, чинит шрифты/тему/русскую кодировку CP1251, управляет заменой/восстановлением sethc.exe и Utilman.exe, а также локальными пользователями.",
            "Restrictions & Recovery (Alt+5)",
            "Unlocks or locks Windows policies, supports custom registry rules, repairs fonts/Aero theme/CP1251 encoding, manages sethc.exe/Utilman.exe overrides, and manages local user accounts."
        ),
        [5] = (
            "Утилиты (Alt+6)",
            "Лаунчер системных утилит Windows, запуск sfc /scannow, управление дисками и запуск портативных .exe из папки Tools\\ (включая запуск под случайным именем).",
            "Utilities Launcher (Alt+6)",
            "Launches built-in Windows tools, runs sfc /scannow, opens disk cleanup, and runs user-supplied .exe tools from Tools\\ (optionally under a randomized name)."
        ),
        [6] = (
            "Редактор реестра (Alt+7)",
            "Прямой просмотр и редактирование кустов реестра (как в живой системе, так и в офлайн-Windows), получение прав владельца (Take Ownership), экспорт и импорт .reg.",
            "Registry Editor (Alt+7)",
            "Browse and edit registry hives (Online & Offline), take ownership of locked keys, and export/import .reg files."
        ),
        [7] = (
            "Встроенный проводник (Alt+8)",
            "Независимый от explorer.exe файловый менеджер: дерево папок, вычисление MD5/SHA256, анализ PE-заголовков, снятие атрибутов Скрытый/Системный, Take Ownership и отложенное удаление заблокированных файлов.",
            "Built-in File Explorer (Alt+8)",
            "Explorer-independent file manager with folder tree, MD5/SHA256 hashing, PE header inspection, unhide Hidden/System attributes, Take Ownership, and MoveFileEx locked deletion."
        ),
        [8] = (
            "Мастер, Отчёт и Журнал (Alt+9)",
            "Автолечение по профилям, сброс файла hosts и политик, журнал всех действий с пошаговым откатом (Undo) и экспорт подробного диагностического отчёта.",
            "Wizard, Report & Log (Alt+9)",
            "Profile-based auto-healing, hosts and policy reset, full action log with step-by-step Undo, and diagnostic report export."
        )
    };

    public static string T(string key)
    {
        if (Strings.TryGetValue(key, out var pair))
        {
            return CurrentLanguage == AppLanguage.Ru ? pair.Ru : pair.En;
        }
        return key;
    }

    public static (string Title, string Body) GetHelpForView(int viewIndex)
    {
        if (HelpTopics.TryGetValue(viewIndex, out var h))
        {
            return CurrentLanguage == AppLanguage.Ru ? (h.TitleRu, h.BodyRu) : (h.TitleEn, h.BodyEn);
        }
        return CurrentLanguage == AppLanguage.Ru
            ? ("Справка Shift Unlocker", "Нажмите Ctrl+K для быстрого перехода к любой функции.")
            : ("Shift Unlocker Help", "Press Ctrl+K to jump to any feature.");
    }

    public static void ToggleLanguage()
    {
        CurrentLanguage = CurrentLanguage == AppLanguage.Ru ? AppLanguage.En : AppLanguage.Ru;
        LanguageChanged?.Invoke();
    }
}
