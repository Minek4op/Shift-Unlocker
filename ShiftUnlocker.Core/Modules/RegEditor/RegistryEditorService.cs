using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;
using ShiftUnlocker.Core.Common;
using ShiftUnlocker.Core.Context;
using ShiftUnlocker.Core.RegHandler;
using ShiftUnlocker.Core.Safety;
using ShiftUnlocker.Native;

namespace ShiftUnlocker.Core.Modules.RegEditor;

public class RegistryEditorService
{
    public static readonly RegistryBookmark[] MalwareBookmarks =
    [
        new("Автозагрузка HKLM (Run)", "HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "Основная ветка автозапуска программ"),
        new("Автозагрузка HKCU (Run)", "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Run", "Автозапуск текущего пользователя"),
        new("Winlogon (Shell/Userinit)", "HKLM", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon", "Подмена оболочки explorer.exe и userinit"),
        new("IFEO (Хиджак утилит)", "HKLM", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options", "Перехват запуска диспетчера задач, regedit и др."),
        new("Политики системы (HKCU)", "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Policies\System", "Блокировки Taskmgr, Regedit, CMD"),
        new("Политики системы (HKLM)", "HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "Блокировки системы на уровне машины и UAC"),
        new("Политики проводника (Explorer)", "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoRun, NoFolderOptions, NoDesktop"),
        new("Службы и драйверы", "HKLM", @"SYSTEM\CurrentControlSet\Services", "Службы Windows, драйверы, руткиты"),
        new("Безопасный режим (SafeBoot)", "HKLM", @"SYSTEM\CurrentControlSet\Control\SafeBoot\Minimal", "Службы, разрешенные в Safe Mode"),
        new("Ассоциации .exe (UserChoice)", "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.exe", "Пользовательские переопределения .exe")
    ];

    public string[] GetSubKeys(ITargetContext ctx, string hive, string subKey)
    {
        return ctx.GetSubKeyNames(hive, subKey);
    }

    public List<RegistryValueDisplay> GetValues(ITargetContext ctx, string hive, string subKey)
    {
        var list = new List<RegistryValueDisplay>();
        using var key = ctx.OpenKey(hive, subKey, false);
        if (key == null) return list;

        foreach (var vName in key.GetValueNames())
        {
            var kind = key.GetValueKind(vName);
            var val = key.GetValue(vName) ?? "";
            string formatted = FormatRegistryValue(val, kind);

            list.Add(new RegistryValueDisplay(
                string.IsNullOrEmpty(vName) ? "(По умолчанию)" : vName,
                kind,
                kind.ToString(),
                val,
                formatted));
        }

        return list;
    }

    private static string FormatRegistryValue(object val, RegistryValueKind kind) => kind switch
    {
        RegistryValueKind.DWord => $"0x{Convert.ToUInt32(val):X8} ({val})",
        RegistryValueKind.QWord => $"0x{Convert.ToUInt64(val):X16} ({val})",
        RegistryValueKind.Binary => string.Join(" ", ((byte[])val).Take(32).Select(b => $"{b:X2}")) + (((byte[])val).Length > 32 ? "..." : ""),
        RegistryValueKind.MultiString => string.Join(" | ", (string[])val),
        _ => val.ToString() ?? ""
    };

    public Result SetValue(ITargetContext ctx, BackupManager bm, string hive, string subKey, string valueName, object val, RegistryValueKind kind)
    {
        return bm.BackupAndSetRegistryValue(ctx, hive, subKey, valueName, val, kind, $"Редактор реестра: изменено {hive}\\{subKey}\\{valueName}");
    }

    public Result DeleteValue(ITargetContext ctx, BackupManager bm, string hive, string subKey, string valueName)
    {
        return bm.BackupAndDeleteRegistryValue(ctx, hive, subKey, valueName, $"Редактор реестра: удалено {hive}\\{subKey}\\{valueName}");
    }

    public Result DeleteKey(ITargetContext ctx, BackupManager bm, string hive, string subKey)
    {
        return bm.BackupAndDeleteSubKeyTree(ctx, hive, subKey, $"Редактор реестра: удален ключ {hive}\\{subKey}");
    }

    public Result ExportKeyToReg(ITargetContext ctx, string hive, string subKey, string targetFilePath)
    {
        try
        {
            var values = new List<RegValueEntry>();
            using var key = ctx.OpenKey(hive, subKey, false);
            if (key != null)
            {
                foreach (var v in key.GetValueNames())
                {
                    var val = key.GetValue(v);
                    if (val != null)
                    {
                        values.Add(new RegValueEntry(v, val, key.GetValueKind(v)));
                    }
                }
            }

            string regContent = RegFileHandler.ExportToRegFormat($"{hive}\\{subKey}", values);
            File.WriteAllText(targetFilePath, regContent, Encoding.Unicode);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail($"Ошибка экспорта в .reg: {ex.Message}");
        }
    }

    public Result ImportRegFile(ITargetContext ctx, BackupManager bm, string regFilePath)
    {
        if (!File.Exists(regFilePath)) return Result.Fail("Файл не найден.");

        try
        {
            string content = File.ReadAllText(regFilePath);
            var entries = RegFileHandler.ParseRegContent(content);

            foreach (var keyEntry in entries)
            {
                // разбираем куст и путь
                int slash = keyEntry.KeyPath.IndexOf('\\');
                if (slash <= 0) continue;

                string hive = keyEntry.KeyPath[..slash];
                string subKey = keyEntry.KeyPath[(slash + 1)..];

                if (keyEntry.IsDeletion)
                {
                    bm.BackupAndDeleteSubKeyTree(ctx, hive, subKey, $"Импорт .reg: удаление {hive}\\{subKey}");
                }
                else
                {
                    foreach (var val in keyEntry.Values)
                    {
                        bm.BackupAndSetRegistryValue(ctx, hive, subKey, val.Name, val.Value, val.Kind, $"Импорт .reg: запись {hive}\\{subKey}\\{val.Name}");
                    }
                }
            }

            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail($"Ошибка импорта .reg: {ex.Message}");
        }
    }

    public Result TakeOwnershipAndGrantAccess(string fullKeyPath)
    {
        try
        {
            // забираем ключ на группу Администраторов
            int slash = fullKeyPath.IndexOf('\\');
            if (slash <= 0) return Result.Fail("Неверный формат ключа.");

            string hiveStr = fullKeyPath[..slash];
            string subKey = fullKeyPath[(slash + 1)..];

            RegistryKey root = hiveStr.ToUpperInvariant() switch
            {
                "HKLM" or "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
                "HKCU" or "HKEY_CURRENT_USER" => Registry.CurrentUser,
                "HKU" or "HKEY_USERS" => Registry.Users,
                "HKCR" or "HKEY_CLASSES_ROOT" => Registry.ClassesRoot,
                _ => Registry.LocalMachine
            };

            using var key = root.OpenSubKey(subKey, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.TakeOwnership);
            if (key != null)
            {
                var sec = key.GetAccessControl();
                var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
                sec.SetOwner(adminSid);
                key.SetAccessControl(sec);

                // выдаем полный доступ
                using var keyControl = root.OpenSubKey(subKey, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.ChangePermissions);
                if (keyControl != null)
                {
                    var acl = keyControl.GetAccessControl();
                    acl.AddAccessRule(new RegistryAccessRule(adminSid, RegistryRights.FullControl, InheritanceFlags.ContainerInherit, PropagationFlags.None, AccessControlType.Allow));
                    keyControl.SetAccessControl(acl);
                }

                return Result.Ok();
            }

            return Result.Fail("Не удалось открыть ключ для смены владельца.");
        }
        catch (Exception ex)
        {
            return Result.Fail($"Ошибка смены владельца ключа: {ex.Message}");
        }
    }
}
