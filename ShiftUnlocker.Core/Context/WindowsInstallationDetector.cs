using System.Diagnostics;
using Microsoft.Win32;

namespace ShiftUnlocker.Core.Context;

public static class WindowsInstallationDetector
{
    public static bool IsRunningInWinPE()
    {
        try
        {
            using var miniNtKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\MiniNT");
            if (miniNtKey != null) return true;

            string systemDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "";
            if (systemDrive.StartsWith("X:", StringComparison.OrdinalIgnoreCase)) return true;
        }
        catch
        {
            // глушим ошибку
        }

        return false;
    }

    public static List<WindowsInstallationInfo> DetectInstallations()
    {
        var installations = new List<WindowsInstallationInfo>();
        bool inWinPE = IsRunningInWinPE();
        string currentSystemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady) continue;

            try
            {
                string winDir = Path.Combine(drive.RootDirectory.FullName, "Windows");
                string system32 = Path.Combine(winDir, "System32");
                string systemHive = Path.Combine(system32, "config", "SYSTEM");

                if (!File.Exists(systemHive) && !File.Exists(Path.Combine(system32, "ntoskrnl.exe")))
                {
                    continue;
                }

                bool isCurrent = !inWinPE && winDir.Equals(currentSystemRoot, StringComparison.OrdinalIgnoreCase);

                string productName = "Windows (Offline)";
                string versionStr = "Unknown";
                DateTime? installDate = null;

                if (isCurrent)
                {
                    try
                    {
                        using var cv = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                        if (cv != null)
                        {
                            productName = cv.GetValue("ProductName")?.ToString() ?? "Windows";
                            versionStr = cv.GetValue("DisplayVersion")?.ToString() ??
                                         cv.GetValue("ReleaseId")?.ToString() ??
                                         cv.GetValue("CurrentVersion")?.ToString() ?? "";
                        }
                    }
                    catch
                    {
                        // пропускаем
                    }
                }
                else
                {
                    // для офлайн-ОС смотрим версию по ntoskrnl или cmd
                    string ntoskrnl = Path.Combine(system32, "ntoskrnl.exe");
                    if (File.Exists(ntoskrnl))
                    {
                        try
                        {
                            var fvi = FileVersionInfo.GetVersionInfo(ntoskrnl);
                            productName = fvi.ProductName ?? "Windows";
                            versionStr = fvi.ProductVersion ?? fvi.FileVersion ?? "";
                        }
                        catch
                        {
                            // пропускаем
                        }
                    }
                }

                try
                {
                    installDate = Directory.GetCreationTime(winDir);
                }
                catch
                {
                    // пропускаем
                }

                installations.Add(new WindowsInstallationInfo(
                    drive.Name,
                    winDir,
                    productName,
                    versionStr,
                    installDate,
                    isCurrent,
                    isCurrent && inWinPE));
            }
            catch
            {
                // нет доступа к диску
            }
        }

        return installations;
    }
}
