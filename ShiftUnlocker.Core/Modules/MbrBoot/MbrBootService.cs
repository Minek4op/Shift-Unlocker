using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using ShiftUnlocker.Core.Common;
using ShiftUnlocker.Core.Safety;
using ShiftUnlocker.Native;

namespace ShiftUnlocker.Core.Modules.MbrBoot;

public class MbrBootService
{
    public List<PhysicalDriveInfo> EnumeratePhysicalDrives()
    {
        var list = new List<PhysicalDriveInfo>();

        for (int i = 0; i < 16; i++)
        {
            string devPath = $@"\\.\PhysicalDrive{i}";
            using var handle = Kernel32.CreateFileW(
                devPath,
                DiskIoControlConstants.GENERIC_READ,
                DiskIoControlConstants.FILE_SHARE_READ | DiskIoControlConstants.FILE_SHARE_WRITE,
                IntPtr.Zero,
                DiskIoControlConstants.OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (handle.IsInvalid) continue;

            long sizeBytes = 0;
            string partStyle = "MBR";
            int partCount = 0;

            const int bufferSize = 16384;
            IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                const uint IOCTL_DISK_GET_LENGTH_INFO = 0x0007405C;
                if (Kernel32.DeviceIoControl(
                    handle,
                    IOCTL_DISK_GET_LENGTH_INFO,
                    IntPtr.Zero,
                    0,
                    buffer,
                    8,
                    out uint lenReturned,
                    IntPtr.Zero) && lenReturned >= 8)
                {
                    sizeBytes = Marshal.ReadInt64(buffer, 0);
                }

                if (Kernel32.DeviceIoControl(
                    handle,
                    DiskIoControlConstants.IOCTL_DISK_GET_DRIVE_LAYOUT_EX,
                    IntPtr.Zero,
                    0,
                    buffer,
                    bufferSize,
                    out uint returned,
                    IntPtr.Zero) && returned >= 8)
                {
                    uint style = unchecked((uint)Marshal.ReadInt32(buffer, 0));
                    uint count = unchecked((uint)Marshal.ReadInt32(buffer, 4));
                    partStyle = style == (uint)PARTITION_STYLE.PARTITION_STYLE_GPT ? "GPT" : "MBR";
                    partCount = (int)count;
                }
            }
            catch { }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            list.Add(new PhysicalDriveInfo(i, devPath, $"Диск {i} ({partStyle})", sizeBytes, partStyle, partCount));
        }

        return list;
    }

    public Result<MbrSectorInfo> ReadMbrSector(int driveIndex)
    {
        string devPath = $@"\\.\PhysicalDrive{driveIndex}";
        using var handle = Kernel32.CreateFileW(
            devPath,
            DiskIoControlConstants.GENERIC_READ,
            DiskIoControlConstants.FILE_SHARE_READ | DiskIoControlConstants.FILE_SHARE_WRITE,
            IntPtr.Zero,
            DiskIoControlConstants.OPEN_EXISTING,
            0,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            return Result<MbrSectorInfo>.Fail($"Не удалось открыть {devPath}: {Win32Error.GetMessage(Marshal.GetLastWin32Error())}");
        }

        byte[] sector = new byte[512];
        if (!Kernel32.ReadFile(handle, sector, 512, out uint read, IntPtr.Zero) || read != 512)
        {
            return Result<MbrSectorInfo>.Fail($"Ошибка чтения сектора 0 с {devPath}: {Win32Error.GetMessage(Marshal.GetLastWin32Error())}");
        }

        return Result<MbrSectorInfo>.Ok(new MbrSectorInfo(sector));
    }

    public Result BackupMbrSector(int driveIndex, string targetFilePath, BackupManager bm)
    {
        var readRes = ReadMbrSector(driveIndex);
        if (!readRes.Success || readRes.Value == null)
        {
            return Result.Fail(readRes.ErrorMessage ?? "Ошибка чтения MBR перед бэкапом");
        }

        try
        {
            string? dir = Path.GetDirectoryName(targetFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllBytes(targetFilePath, readRes.Value.RawBytes);
            bm.Log("MBR", $"Создан резервный бэкап MBR диска {driveIndex} в {targetFilePath}");
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail($"Ошибка сохранения бэкапа MBR: {ex.Message}");
        }
    }

    public Result RestoreMbrFromFile(int driveIndex, string backupFilePath, BackupManager bm)
    {
        if (!File.Exists(backupFilePath))
        {
            return Result.Fail($"Файл бэкапа не найден: {backupFilePath}");
        }

        byte[] backupBytes;
        try
        {
            backupBytes = File.ReadAllBytes(backupFilePath);
        }
        catch (Exception ex)
        {
            return Result.Fail($"Ошибка чтения файла бэкапа: {ex.Message}");
        }

        if (backupBytes.Length < 512)
        {
            return Result.Fail("Файл бэкапа поврежден: размер меньше 512 байт!");
        }

        if (backupBytes[510] != 0x55 || backupBytes[511] != 0xAA)
        {
            return Result.Fail("Файл бэкапа не содержит сигнатуру 0x55AA!");
        }

        string preRestoreBackup = Path.Combine(bm.SessionFolderPath, $"PreRestore_Drive{driveIndex}_{DateTime.Now:HHmmss}.bin");
        var preBackupRes = BackupMbrSector(driveIndex, preRestoreBackup, bm);
        if (!preBackupRes.Success)
        {
            return Result.Fail($"Не удалось создать контрольный бэкап перед записью: {preBackupRes.ErrorMessage}");
        }

        string devPath = $@"\\.\PhysicalDrive{driveIndex}";
        using var handle = Kernel32.CreateFileW(
            devPath,
            DiskIoControlConstants.GENERIC_READ | DiskIoControlConstants.GENERIC_WRITE,
            DiskIoControlConstants.FILE_SHARE_READ | DiskIoControlConstants.FILE_SHARE_WRITE,
            IntPtr.Zero,
            DiskIoControlConstants.OPEN_EXISTING,
            0,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            return Result.Fail($"Не удалось открыть {devPath} на запись: {Win32Error.GetMessage(Marshal.GetLastWin32Error())}");
        }

        byte[] writeSector = backupBytes[..512];
        if (!Kernel32.WriteFile(handle, writeSector, 512, out uint written, IntPtr.Zero) || written != 512)
        {
            return Result.Fail($"Ошибка записи сектора 0 на {devPath}: {Win32Error.GetMessage(Marshal.GetLastWin32Error())}");
        }

        bm.Log("MBR", $"Успешно восстановлен MBR диска {driveIndex} из {backupFilePath}");
        return Result.Ok();
    }

    public bool IsSystemToolAvailable(string toolName)
    {
        string system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
        string path = Path.Combine(system32, toolName);
        if (File.Exists(path)) return true;

        string? envPath = Environment.GetEnvironmentVariable("PATH");
        if (envPath != null)
        {
            foreach (var p in envPath.Split(';'))
            {
                if (!string.IsNullOrWhiteSpace(p) && File.Exists(Path.Combine(p.Trim(), toolName)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public async Task<Result> RunBootsectAsync(string driveLetter)
    {
        if (!IsSystemToolAvailable("bootsect.exe"))
        {
            return Result.Fail("Утилита bootsect.exe не найдена в системе/WinPE.");
        }

        string cleanDrive = driveLetter.TrimEnd('\\');
        return await RunProcessCaptureAsync("bootsect.exe", $"/nt60 {cleanDrive} /mbr");
    }

    public async Task<Result> RunBcdbootAsync(string windowsDirectory, string? espDrive)
    {
        if (!IsSystemToolAvailable("bcdboot.exe"))
        {
            return Result.Fail("Утилита bcdboot.exe не найдена в системе/WinPE.");
        }

        string args = $"\"{windowsDirectory}\"";
        if (!string.IsNullOrWhiteSpace(espDrive))
        {
            args += $" /s {espDrive.TrimEnd('\\')}";
        }

        return await RunProcessCaptureAsync("bcdboot.exe", args);
    }

    public async Task<Result> RunSfcOfflineAsync(string offlineWindir, string offlineBootdir)
    {
        if (!IsSystemToolAvailable("sfc.exe"))
        {
            return Result.Fail("Утилита sfc.exe не найдена в системе/WinPE.");
        }

        string args = $"/scannow /offwindir=\"{offlineWindir}\" /offbootdir=\"{offlineBootdir}\"";
        return await RunProcessCaptureAsync("sfc.exe", args);
    }

    public async Task<Result> RunDismOfflineAsync(string offlineWindir)
    {
        if (!IsSystemToolAvailable("dism.exe"))
        {
            return Result.Fail("Утилита dism.exe не найдена в системе/WinPE.");
        }

        string args = $"/Image:\"{offlineWindir}\" /Cleanup-Image /RestoreHealth";
        return await RunProcessCaptureAsync("dism.exe", args);
    }

    private static async Task<Result> RunProcessCaptureAsync(string fileName, string args)
    {
        return await Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var p = Process.Start(psi);
                if (p == null) return Result.Fail($"Не удалось запустить {fileName}");

                string stdout = p.StandardOutput.ReadToEnd();
                string stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();

                if (p.ExitCode == 0)
                {
                    return Result.Ok();
                }
                else
                {
                    return Result.Fail($"Команда завершилась с кодом {p.ExitCode}:\n{stdout}\n{stderr}");
                }
            }
            catch (Exception ex)
            {
                return Result.Fail($"Ошибка запуска {fileName}: {ex.Message}");
            }
        });
    }
}
