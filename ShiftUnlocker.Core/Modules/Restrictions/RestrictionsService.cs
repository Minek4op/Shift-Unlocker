using System.Text.Json;
using Microsoft.Win32;
using ShiftUnlocker.Core.Common;
using ShiftUnlocker.Core.Context;
using ShiftUnlocker.Core.Safety;

namespace ShiftUnlocker.Core.Modules.Restrictions;

public enum RestrictionStatus
{
    Allowed,
    Restricted,
    NotSet
}

public class CustomRestrictionDto
{
    public string Id { get; set; } = "";
    public string Category { get; set; } = "Пользовательские";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Hive { get; set; } = "HKCU";
    public string SubKey { get; set; } = "";
    public string ValueName { get; set; } = "";
    public string RestrictedValue { get; set; } = "1";
    public string AllowedValue { get; set; } = "0";
    public string ValueKind { get; set; } = "DWord";
    public bool DeleteToAllow { get; set; } = true;
}

public class RestrictionItem
{
    public string Id { get; }
    public string Category { get; }
    public string Title { get; }
    public string Description { get; }
    public string Hive { get; }
    public string SubKey { get; }
    public string ValueName { get; }
    public object RestrictedValue { get; }
    public object AllowedValue { get; }
    public RegistryValueKind ValueKind { get; }
    public bool DeleteToAllow { get; }
    public bool IsCustom { get; }

    public RestrictionStatus CurrentStatus { get; set; } = RestrictionStatus.NotSet;
    public object? CurrentRawValue { get; set; }

    public RestrictionItem(
        string id,
        string category,
        string title,
        string description,
        string hive,
        string subKey,
        string valueName,
        object restrictedValue,
        object allowedValue,
        RegistryValueKind valueKind = RegistryValueKind.DWord,
        bool deleteToAllow = true,
        bool isCustom = false)
    {
        Id = id;
        Category = category;
        Title = title;
        Description = description;
        Hive = hive;
        SubKey = subKey;
        ValueName = valueName;
        RestrictedValue = restrictedValue;
        AllowedValue = allowedValue;
        ValueKind = valueKind;
        DeleteToAllow = deleteToAllow;
        IsCustom = isCustom;
    }

    public void Refresh(ITargetContext ctx)
    {
        CurrentRawValue = ctx.GetValue(Hive, SubKey, ValueName);
        if (CurrentRawValue == null)
        {
            CurrentStatus = RestrictionStatus.NotSet;
            return;
        }

        string rawStr = CurrentRawValue.ToString() ?? "";
        string restrictedStr = RestrictedValue.ToString() ?? "";

        if (rawStr.Equals(restrictedStr, StringComparison.OrdinalIgnoreCase))
        {
            CurrentStatus = RestrictionStatus.Restricted;
        }
        else
        {
            CurrentStatus = RestrictionStatus.Allowed;
        }
    }

    public Result Unlock(ITargetContext ctx, BackupManager bm)
    {
        if (DeleteToAllow)
        {
            return bm.BackupAndDeleteRegistryValue(ctx, Hive, SubKey, ValueName, $"Unlocked {Title}");
        }
        else
        {
            return bm.BackupAndSetRegistryValue(ctx, Hive, SubKey, ValueName, AllowedValue, ValueKind, $"Set {Title} to allowed value");
        }
    }

    public Result Lock(ITargetContext ctx, BackupManager bm)
    {
        return bm.BackupAndSetRegistryValue(ctx, Hive, SubKey, ValueName, RestrictedValue, ValueKind, $"Locked/Restricted {Title}");
    }
}

public class RestrictionsService
{
    private readonly string _customConfigPath;

    public RestrictionsService(string? baseDir = null)
    {
        string root = baseDir ?? AppDomain.CurrentDomain.BaseDirectory;
        _customConfigPath = Path.Combine(root, "custom_restrictions.json");
    }
    public List<RestrictionItem> CreateStandardRestrictions()
    {
        return new List<RestrictionItem>
        {
            // инструменты
            new RestrictionItem("tools_taskmgr", "Инструменты", "Диспетчер задач",
                "Блокировка запуска Taskmgr.exe через DisableTaskMgr",
                "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Policies\System", "DisableTaskMgr", 1, 0),

            new RestrictionItem("tools_taskmgr_lm", "Инструменты", "Диспетчер задач (HKLM)",
                "Блокировка диспетчера задач на уровне машины",
                "HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "DisableTaskMgr", 1, 0),

            new RestrictionItem("tools_regedit", "Инструменты", "Редактор реестра",
                "Блокировка запуска Regedit.exe через DisableRegistryTools",
                "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Policies\System", "DisableRegistryTools", 1, 0),

            new RestrictionItem("tools_regedit_lm", "Инструменты", "Редактор реестра (HKLM)",
                "Блокировка редактора реестра на уровне машины",
                "HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "DisableRegistryTools", 1, 0),

            new RestrictionItem("tools_cmd", "Инструменты", "Командная строка",
                "Блокировка запуска CMD.exe через DisableCMD",
                "HKCU", @"Software\Policies\Microsoft\Windows\System", "DisableCMD", 1, 0),

            new RestrictionItem("tools_cmd_lm", "Инструменты", "Командная строка (HKLM)",
                "Блокировка командной строки на уровне машины",
                "HKLM", @"SOFTWARE\Policies\Microsoft\Windows\System", "DisableCMD", 1, 0),

            new RestrictionItem("tools_norun", "Инструменты", "Диалог «Выполнить»",
                "Отключение диалога «Выполнить» (Win+R) через NoRun",
                "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoRun", 1, 0),

            new RestrictionItem("tools_nofolderopt", "Инструменты", "Параметры папок",
                "Скрытие вкладки «Параметры папок» через NoFolderOptions",
                "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoFolderOptions", 1, 0),

            new RestrictionItem("tools_nodesktop", "Инструменты", "Рабочий стол",
                "Скрытие значков рабочего стола через NoDesktop",
                "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoDesktop", 1, 0),

            new RestrictionItem("tools_nocontext", "Инструменты", "Контекстное меню Проводника",
                "Отключение контекстного меню правой кнопки мыши через NoViewContextMenu",
                "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoViewContextMenu", 1, 0),

            new RestrictionItem("tools_nowinkeys", "Инструменты", "Клавиши Windows (Win+...)",
                "Отключение комбинаций клавиш Win через NoWinKeys",
                "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoWinKeys", 1, 0),

            new RestrictionItem("tools_nodrives", "Инструменты", "Скрытие дисков",
                "Скрытие всех или выбранных дисков через NoDrives",
                "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoDrives", 0x3FFFFFF, 0),

            // оболочка и вход
            new RestrictionItem("shell_lockws", "Оболочка", "Блокировка рабочей станции",
                "Отключение блокировки компьютера через DisableLockWorkstation",
                "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Policies\System", "DisableLockWorkstation", 1, 0),

            new RestrictionItem("shell_changepass", "Оболочка", "Смена пароля",
                "Отключение пункта смены пароля в меню безопасности (Ctrl+Alt+Del)",
                "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Policies\System", "DisableChangePassword", 1, 0),

            new RestrictionItem("shell_noclose", "Оболочка", "Завершение работы Windows",
                "Отключение пунктов выключения и перезагрузки через NoClose",
                "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoClose", 1, 0),

            new RestrictionItem("shell_nologoff", "Оболочка", "Выход из системы",
                "Отключение пункта выхода из системы через NoLogoff",
                "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoLogoff", 1, 0),

            // защита
            new RestrictionItem("sec_defender_lm", "Защита", "Windows Defender (Антишпион)",
                "Отключение антивируса через DisableAntiSpyware",
                "HKLM", @"SOFTWARE\Policies\Microsoft\Windows Defender", "DisableAntiSpyware", 1, 0),

            new RestrictionItem("sec_defender_realtime", "Защита", "Windows Defender (Realtime)",
                "Отключение защиты в реальном времени Defender",
                "HKLM", @"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection", "DisableRealtimeMonitoring", 1, 0),

            new RestrictionItem("sec_uac_lua", "Защита", "UAC (Контроль учетных записей)",
                "Отключение механизма UAC через EnableLUA=0",
                "HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLUA", 0, 1, RegistryValueKind.DWord, false),

            new RestrictionItem("sec_sr_disable", "Защита", "Восстановление системы",
                "Блокировка точек восстановления через DisableSR",
                "HKLM", @"SOFTWARE\Policies\Microsoft\Windows NT\SystemRestore", "DisableSR", 1, 0),

            new RestrictionItem("sec_sr_config", "Защита", "Настройка восстановления системы",
                "Блокировка настройки восстановления через DisableConfig",
                "HKLM", @"SOFTWARE\Policies\Microsoft\Windows NT\SystemRestore", "DisableConfig", 1, 0),

            // накопители
            new RestrictionItem("storage_usbstor", "Накопители", "USB-накопители (USBSTOR)",
                "Блокировка USB-флешек через службу USBSTOR (Start=4)",
                "HKLM", @"SYSTEM\CurrentControlSet\Services\USBSTOR", "Start", 4, 3, RegistryValueKind.DWord, false),

            new RestrictionItem("storage_writeprotect", "Накопители", "Защита от записи USB",
                "Принудительный режим только для чтения для всех USB через WriteProtect",
                "HKLM", @"SYSTEM\CurrentControlSet\Control\StorageDevicePolicies", "WriteProtect", 1, 0),

            new RestrictionItem("storage_autorun", "Накопители", "Автозапуск со съемных дисков",
                "Автозапуск со съемных дисков через NoDriveTypeAutoRun",
                "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoDriveTypeAutoRun", 0, 0xFF),

            // файлы и проводник
            new RestrictionItem("files_hidden", "Файлы", "Скрытые файлы (Hidden)",
                "Вирус заблокировал отображение скрытых файлов (Hidden=2)",
                "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Hidden", 2, 1, RegistryValueKind.DWord, false),

            new RestrictionItem("files_superhidden", "Файлы", "Системные файлы (SuperHidden)",
                "Вирус заблокировал отображение защищенных системных файлов (ShowSuperHidden=0)",
                "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowSuperHidden", 0, 1, RegistryValueKind.DWord, false),

            new RestrictionItem("files_hideext", "Файлы", "Расширения файлов (HideFileExt)",
                "Скрытие расширений файлов, позволяющее маскировать .exe под .txt/.pdf",
                "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", 1, 0, RegistryValueKind.DWord, false)
        };
    }

    public List<RestrictionItem> LoadCustomRestrictions()
    {
        var list = new List<RestrictionItem>();
        if (!File.Exists(_customConfigPath)) return list;

        try
        {
            string json = File.ReadAllText(_customConfigPath);
            var dtos = JsonSerializer.Deserialize<List<CustomRestrictionDto>>(json);
            if (dtos != null)
            {
                foreach (var dto in dtos)
                {
                    var kind = string.Equals(dto.ValueKind, "String", StringComparison.OrdinalIgnoreCase)
                        ? RegistryValueKind.String
                        : RegistryValueKind.DWord;

                    object restrictedVal = kind == RegistryValueKind.DWord && int.TryParse(dto.RestrictedValue, out int rInt)
                        ? rInt
                        : dto.RestrictedValue;
                    object allowedVal = kind == RegistryValueKind.DWord && int.TryParse(dto.AllowedValue, out int aInt)
                        ? aInt
                        : dto.AllowedValue;

                    list.Add(new RestrictionItem(
                        dto.Id,
                        string.IsNullOrWhiteSpace(dto.Category) ? "Пользовательские" : dto.Category,
                        dto.Title,
                        dto.Description,
                        dto.Hive,
                        dto.SubKey,
                        dto.ValueName,
                        restrictedVal,
                        allowedVal,
                        kind,
                        dto.DeleteToAllow,
                        isCustom: true));
                }
            }
        }
        catch { }

        return list;
    }

    public Result AddCustomRestriction(CustomRestrictionDto dto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.SubKey) || string.IsNullOrWhiteSpace(dto.ValueName))
            {
                return Result.Fail("Заполните название, путь к ключу и имя параметра.");
            }

            if (string.IsNullOrWhiteSpace(dto.Id))
            {
                dto.Id = "custom_" + Guid.NewGuid().ToString("N")[..8];
            }

            var existing = new List<CustomRestrictionDto>();
            if (File.Exists(_customConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(_customConfigPath);
                    existing = JsonSerializer.Deserialize<List<CustomRestrictionDto>>(json) ?? new();
                }
                catch { }
            }

            existing.Add(dto);
            File.WriteAllText(_customConfigPath, JsonSerializer.Serialize(existing, new JsonSerializerOptions { WriteIndented = true }));
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail($"Ошибка сохранения пользовательского ограничения: {ex.Message}");
        }
    }

    public Result RemoveCustomRestriction(string id)
    {
        try
        {
            if (!File.Exists(_customConfigPath)) return Result.Fail("Файл пользовательских ограничений не найден.");

            string json = File.ReadAllText(_customConfigPath);
            var existing = JsonSerializer.Deserialize<List<CustomRestrictionDto>>(json) ?? new();
            int removed = existing.RemoveAll(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            if (removed == 0) return Result.Fail("Пользовательское ограничение не найдено.");

            File.WriteAllText(_customConfigPath, JsonSerializer.Serialize(existing, new JsonSerializerOptions { WriteIndented = true }));
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail($"Ошибка удаления: {ex.Message}");
        }
    }

    public List<RestrictionItem> CheckAll(ITargetContext ctx)
    {
        var items = CreateStandardRestrictions();
        items.AddRange(LoadCustomRestrictions());
        foreach (var item in items)
        {
            item.Refresh(ctx);
        }
        return items;
    }

    public Result UnlockAll(ITargetContext ctx, BackupManager bm, IEnumerable<RestrictionItem> items)
    {
        int failed = 0;
        foreach (var item in items)
        {
            if (item.CurrentStatus == RestrictionStatus.Restricted)
            {
                var res = item.Unlock(ctx, bm);
                if (!res.Success) failed++;
            }
        }
        return failed == 0 ? Result.Ok() : Result.Fail($"{failed} restrictions could not be removed.");
    }
}
