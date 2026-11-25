using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using ShiftUnlocker.Core.Common;
using ShiftUnlocker.Core.Safety;
using ShiftUnlocker.Native;

namespace ShiftUnlocker.Core.Modules.FileManager;

public class FileManagerService
{
    public List<FileSystemItem> ListDirectory(string directoryPath)
    {
        var list = new List<FileSystemItem>();
        if (!Directory.Exists(directoryPath)) return list;

        var dirInfo = new DirectoryInfo(directoryPath);

        try
        {
            foreach (var d in dirInfo.GetDirectories())
            {
                bool hiddenOrSys = d.Attributes.HasFlag(FileAttributes.Hidden) || d.Attributes.HasFlag(FileAttributes.System);
                list.Add(new FileSystemItem(
                    d.Name,
                    d.FullName,
                    true,
                    0,
                    d.LastWriteTime,
                    d.Attributes,
                    FormatAttributes(d.Attributes),
                    hiddenOrSys));
            }
        }
        catch { }

        try
        {
            foreach (var f in dirInfo.GetFiles())
            {
                bool hiddenOrSys = f.Attributes.HasFlag(FileAttributes.Hidden) || f.Attributes.HasFlag(FileAttributes.System);
                list.Add(new FileSystemItem(
                    f.Name,
                    f.FullName,
                    false,
                    f.Length,
                    f.LastWriteTime,
                    f.Attributes,
                    FormatAttributes(f.Attributes),
                    hiddenOrSys));
            }
        }
        catch { }

        return list;
    }

    private static string FormatAttributes(FileAttributes attrs)
    {
        var sb = new System.Text.StringBuilder();
        if (attrs.HasFlag(FileAttributes.Directory)) sb.Append('D');
        if (attrs.HasFlag(FileAttributes.ReadOnly)) sb.Append('R');
        if (attrs.HasFlag(FileAttributes.Hidden)) sb.Append('H');
        if (attrs.HasFlag(FileAttributes.System)) sb.Append('S');
        if (attrs.HasFlag(FileAttributes.Archive)) sb.Append('A');
        return sb.ToString();
    }

    public (string Md5, string Sha1, string Sha256) CalculateHashes(string filePath)
    {
        if (!File.Exists(filePath)) return ("", "", "");

        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var md5 = MD5.Create();
            using var sha1 = SHA1.Create();
            using var sha256 = SHA256.Create();

            byte[] md5Hash = md5.ComputeHash(fs);
            fs.Seek(0, SeekOrigin.Begin);
            byte[] sha1Hash = sha1.ComputeHash(fs);
            fs.Seek(0, SeekOrigin.Begin);
            byte[] sha256Hash = sha256.ComputeHash(fs);

            return (
                Convert.ToHexString(md5Hash).ToLowerInvariant(),
                Convert.ToHexString(sha1Hash).ToLowerInvariant(),
                Convert.ToHexString(sha256Hash).ToLowerInvariant());
        }
        catch
        {
            return ("Error", "Error", "Error");
        }
    }

    public Result DeleteLockedFile(string filePath, BackupManager bm)
    {
        if (!File.Exists(filePath)) return Result.Fail("Файл не существует.");

        // сначала бэкапим файл
        bm.BackupFile(filePath);

        // сбрасываем атрибуты (скрытый, системный, рид-онли)
        try
        {
            File.SetAttributes(filePath, FileAttributes.Normal);
        }
        catch { }

        // пробуем удалить напрямую
        try
        {
            File.Delete(filePath);
            bm.Log("FileSystem", $"Удален файл: {filePath}");
            return Result.Ok();
        }
        catch
        {
            // не вышло — забираем права
        }

        // меняем владельца и выдаем полный доступ
        TakeOwnershipOfPath(filePath);

        try
        {
            File.SetAttributes(filePath, FileAttributes.Normal);
            File.Delete(filePath);
            bm.Log("FileSystem", $"Удален защищенный файл (после снятия прав): {filePath}");
            return Result.Ok();
        }
        catch
        {
            // файл занят — ставим на удаление при ребуте
            if (Kernel32.MoveFileExW(filePath, null, Kernel32.MOVEFILE_DELAY_UNTIL_REBOOT))
            {
                bm.Log("FileSystem", $"Файл занят процессом. Запланировано удаление при перезагрузке: {filePath}");
                return Result.Ok();
            }

            int lastErr = Marshal.GetLastWin32Error();
            return Result.Fail($"Не удалось удалить файл: {Win32Error.GetMessage(lastErr)}");
        }
    }

    public Result TakeOwnershipOfPath(string path)
    {
        try
        {
            var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            if (File.Exists(path))
            {
                var fileInfo = new FileInfo(path);
                var sec = fileInfo.GetAccessControl();
                sec.SetOwner(adminSid);
                fileInfo.SetAccessControl(sec);

                sec.AddAccessRule(new FileSystemAccessRule(adminSid, FileSystemRights.FullControl, AccessControlType.Allow));
                fileInfo.SetAccessControl(sec);
                return Result.Ok();
            }
            else if (Directory.Exists(path))
            {
                var dirInfo = new DirectoryInfo(path);
                var sec = dirInfo.GetAccessControl();
                sec.SetOwner(adminSid);
                dirInfo.SetAccessControl(sec);

                sec.AddAccessRule(new FileSystemAccessRule(adminSid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
                dirInfo.SetAccessControl(sec);
                return Result.Ok();
            }

            return Result.Fail("Путь не найден.");
        }
        catch (Exception ex)
        {
            return Result.Fail($"Ошибка получения прав: {ex.Message}");
        }
    }

    public Result UnhideItem(string path, bool recursive = false)
    {
        try
        {
            if (File.Exists(path))
            {
                var attrs = File.GetAttributes(path);
                attrs &= ~FileAttributes.Hidden;
                attrs &= ~FileAttributes.System;
                if (attrs == 0) attrs = FileAttributes.Normal;
                File.SetAttributes(path, attrs);
                return Result.Ok();
            }
            else if (Directory.Exists(path))
            {
                var dInfo = new DirectoryInfo(path);
                var attrs = dInfo.Attributes;
                attrs &= ~FileAttributes.Hidden;
                attrs &= ~FileAttributes.System;
                if (attrs == 0) attrs = FileAttributes.Normal;
                dInfo.Attributes = attrs;

                if (recursive)
                {
                    foreach (var f in dInfo.GetFiles("*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            var fAttrs = f.Attributes & ~FileAttributes.Hidden & ~FileAttributes.System;
                            if (fAttrs == 0) fAttrs = FileAttributes.Normal;
                            f.Attributes = fAttrs;
                        }
                        catch { }
                    }
                    foreach (var subDir in dInfo.GetDirectories("*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            var sAttrs = subDir.Attributes & ~FileAttributes.Hidden & ~FileAttributes.System;
                            if (sAttrs == 0) sAttrs = FileAttributes.Normal;
                            subDir.Attributes = sAttrs;
                        }
                        catch { }
                    }
                }
                return Result.Ok();
            }

            return Result.Fail("Элемент не найден.");
        }
        catch (Exception ex)
        {
            return Result.Fail($"Ошибка снятия атрибутов: {ex.Message}");
        }
    }

    public Result CreateFolder(string parentDir, string folderName)
    {
        try
        {
            if (!Directory.Exists(parentDir)) return Result.Fail("Родительский каталог не существует.");
            if (string.IsNullOrWhiteSpace(folderName)) return Result.Fail("Имя папки не может быть пустым.");

            string newPath = Path.Combine(parentDir, folderName);
            if (Directory.Exists(newPath)) return Result.Fail("Папка с таким именем уже существует.");

            Directory.CreateDirectory(newPath);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail($"Ошибка создания папки: {ex.Message}");
        }
    }

    public Result RenameItem(string oldPath, string newName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(newName)) return Result.Fail("Новое имя не может быть пустым.");

            if (File.Exists(oldPath))
            {
                string? parent = Path.GetDirectoryName(oldPath);
                if (parent == null) return Result.Fail("Неверный путь файла.");
                string newPath = Path.Combine(parent, newName);
                if (File.Exists(newPath)) return Result.Fail("Файл с таким именем уже существует.");
                File.Move(oldPath, newPath);
                return Result.Ok();
            }
            else if (Directory.Exists(oldPath))
            {
                string? parent = Path.GetDirectoryName(oldPath.TrimEnd('\\', '/'));
                if (parent == null) return Result.Fail("Неверный путь папки.");
                string newPath = Path.Combine(parent, newName);
                if (Directory.Exists(newPath)) return Result.Fail("Папка с таким именем уже существует.");
                Directory.Move(oldPath, newPath);
                return Result.Ok();
            }

            return Result.Fail("Элемент для переименования не найден.");
        }
        catch (Exception ex)
        {
            return Result.Fail($"Ошибка переименования: {ex.Message}");
        }
    }

    public Result DeleteItem(string path, bool recursive = true)
    {
        try
        {
            if (File.Exists(path))
            {
                // снимаем только-чтение перед удалением
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
                return Result.Ok();
            }
            else if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive);
                return Result.Ok();
            }

            return Result.Fail("Элемент не найден.");
        }
        catch (Exception ex)
        {
            return Result.Fail($"Ошибка удаления: {ex.Message}");
        }
    }
}
