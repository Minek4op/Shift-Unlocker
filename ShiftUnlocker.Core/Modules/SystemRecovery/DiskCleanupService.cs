using System.Diagnostics;
using ShiftUnlocker.Core.Common;

namespace ShiftUnlocker.Core.Modules.SystemRecovery;

public record DiskVolumeItem(
    string VolumeIndex,
    string DriveLetter,
    string Label,
    string FileSystem,
    string Type,
    string Size,
    string Status,
    string Info);

public class DiskCleanupService
{
    public List<DiskVolumeItem> GetVolumes()
    {
        var list = new List<DiskVolumeItem>();
        try
        {
            string scriptPath = Path.Combine(Path.GetTempPath(), "su_listvol.txt");
            File.WriteAllText(scriptPath, "list volume\nexit\n");

            var psi = new ProcessStartInfo
            {
                FileName = "diskpart.exe",
                Arguments = $"/s \"{scriptPath}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.GetEncoding(866)
            };

            using var p = Process.Start(psi);
            if (p != null)
            {
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(6000);
                try { File.Delete(scriptPath); } catch { }

                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                bool startParsing = false;

                foreach (var line in lines)
                {
                    string l = line.Trim();
                    if (l.StartsWith("---") || l.Contains("Том ###") || l.Contains("Volume ###"))
                    {
                        startParsing = true;
                        continue;
                    }
                    if (startParsing && (l.StartsWith("Том ") || l.StartsWith("Volume ")))
                    {
                        // пример строки вывода diskpart
                        var parts = System.Text.RegularExpressions.Regex.Split(l, @"\s{2,}");
                        string volIdx = parts.Length > 0 ? parts[0] : "";
                        string letter = parts.Length > 1 ? parts[1] : "";
                        string label = parts.Length > 2 ? parts[2] : "";
                        string fs = parts.Length > 3 ? parts[3] : "";
                        string type = parts.Length > 4 ? parts[4] : "";
                        string size = parts.Length > 5 ? parts[5] : "";
                        string status = parts.Length > 6 ? parts[6] : "";
                        string info = parts.Length > 7 ? parts[7] : "";

                        list.Add(new DiskVolumeItem(volIdx, letter, label, fs, type, size, status, info));
                    }
                }
            }
        }
        catch { }

        // запасной вариант через DriveInfo
        if (list.Count == 0)
        {
            foreach (var d in DriveInfo.GetDrives())
            {
                if (d.IsReady)
                {
                    list.Add(new DiskVolumeItem(
                        "-",
                        d.Name.TrimEnd('\\'),
                        d.VolumeLabel,
                        d.DriveFormat,
                        d.DriveType.ToString(),
                        $"{d.TotalSize / (1024 * 1024 * 1024.0):F1} ГБ",
                        "Готов",
                        $"Свободно: {d.TotalFreeSpace / (1024 * 1024 * 1024.0):F1} ГБ"));
                }
            }
        }

        return list;
    }

    public Result DeleteVolume(string volumeIdentifier)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(volumeIdentifier)) return Result.Fail("Не указан том для удаления.");

            // вытаскиваем номер тома или букву
            string target = volumeIdentifier.Trim();
            if (target.StartsWith("Том ", StringComparison.OrdinalIgnoreCase) || target.StartsWith("Volume ", StringComparison.OrdinalIgnoreCase))
            {
                var match = System.Text.RegularExpressions.Regex.Match(target, @"\d+");
                if (match.Success) target = match.Value;
            }

            string scriptPath = Path.Combine(Path.GetTempPath(), "su_delvol.txt");
            string cmd = target.Length == 1 && char.IsLetter(target[0])
                ? $"select volume {target}\ndelete volume\nexit\n"
                : $"select volume {target}\ndelete volume\nexit\n";

            File.WriteAllText(scriptPath, cmd);

            var psi = new ProcessStartInfo
            {
                FileName = "diskpart.exe",
                Arguments = $"/s \"{scriptPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.GetEncoding(866)
            };

            using var p = Process.Start(psi);
            if (p == null) return Result.Fail("Не удалось запустить DiskPart.");
            string outText = p.StandardOutput.ReadToEnd();
            string errText = p.StandardError.ReadToEnd();
            p.WaitForExit(10000);
            try { File.Delete(scriptPath); } catch { }

            string combined = outText + " " + errText;
            if (combined.Contains("успешно") || combined.Contains("successfully"))
            {
                return Result.Ok();
            }
            return Result.Fail($"Результат DiskPart: {combined.Trim()}");
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    public Result CleanDisk(int diskNumber)
    {
        try
        {
            string scriptPath = Path.Combine(Path.GetTempPath(), "su_cleandisk.txt");
            string cmd = $"select disk {diskNumber}\nclean\nexit\n";
            File.WriteAllText(scriptPath, cmd);

            var psi = new ProcessStartInfo
            {
                FileName = "diskpart.exe",
                Arguments = $"/s \"{scriptPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.GetEncoding(866)
            };

            using var p = Process.Start(psi);
            if (p == null) return Result.Fail("Не удалось запустить DiskPart.");
            string outText = p.StandardOutput.ReadToEnd();
            string errText = p.StandardError.ReadToEnd();
            p.WaitForExit(10000);
            try { File.Delete(scriptPath); } catch { }

            string combined = outText + " " + errText;
            if (combined.Contains("успешно") || combined.Contains("successfully"))
            {
                return Result.Ok();
            }
            return Result.Fail($"Результат DiskPart: {combined.Trim()}");
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    public static void OpenDiskManagement()
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = "diskmgmt.msc", UseShellExecute = true });
        }
        catch
        {
            try { Process.Start(new ProcessStartInfo { FileName = "diskpart.exe", UseShellExecute = true }); } catch { }
        }
    }
}
