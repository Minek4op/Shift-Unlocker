using System.Diagnostics;
using System.Security.Cryptography;

namespace ShiftUnlocker.Core.Security;

public static class RandomLauncher
{
    public const string ArgSpawned = "--spawned-random";
    public const string ArgOriginalPath = "--original-exe=";

    public static bool IsRandomlyNamed(string exePath)
    {
        string fileName = Path.GetFileNameWithoutExtension(exePath);
        // стандартные имена не считаем случайными
        if (fileName.Equals("ShiftUnlocker", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("ShiftUnlocker.App", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // проверяем формат сгенерированного имени
        if (fileName.Length is >= 8 and <= 24 && fileName.All(char.IsLetterOrDigit))
        {
            return true;
        }

        return false;
    }

    public static string GenerateRandomName(int length = 10)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        byte[] randomBytes = new byte[length];
        RandomNumberGenerator.Fill(randomBytes);

        char[] result = new char[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = chars[randomBytes[i] % chars.Length];
        }

        return new string(result);
    }

    public static string? TrySpawnRandomClone(string[] args)
    {
        // клон уже запущен, второй раз не плодим
        if (args.Any(a => a.Equals(ArgSpawned, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        string currentExe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
        if (string.IsNullOrEmpty(currentExe) || !File.Exists(currentExe))
        {
            return null;
        }

        if (IsRandomlyNamed(currentExe))
        {
            return null; // Already random
        }

        // куда пробуем скопироваться
        string[] candidateDirs =
        [
            Path.GetTempPath(),
            Path.GetDirectoryName(currentExe) ?? "",
            @"X:\Windows\Temp",
            @"C:\Windows\Temp"
        ];

        string? targetDir = null;
        foreach (string dir in candidateDirs)
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            try
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string testFile = Path.Combine(dir, $"su_test_{Guid.NewGuid():N}.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);

                targetDir = dir;
                break;
            }
            catch
            {
                // сюда нельзя писать, берем следующую папку
            }
        }

        if (targetDir == null)
        {
            return null;
        }

        string randomName = GenerateRandomName(17);
        string randomExeName = randomName + ".exe";
        string targetExePath = Path.Combine(targetDir, randomExeName);

        try
        {
            File.Copy(currentExe, targetExePath, true);

            // перебиваем строки версии прямо в копии exe,
            // чтобы диспетчер задач показывал случайное имя
            PatchVersionStrings(targetExePath, randomName);

            var psi = new ProcessStartInfo
            {
                FileName = targetExePath,
                UseShellExecute = true,
                Arguments = $"{ArgSpawned} \"{ArgOriginalPath}{currentExe}\""
            };

            Process.Start(psi);
            return targetExePath;
        }
        catch
        {
            return null;
        }
    }

    public static void PatchVersionStrings(string exePath, string randomName)
    {
        try
        {
            if (string.IsNullOrEmpty(randomName) || randomName.Length < 17) return;

            byte[] bytes = File.ReadAllBytes(exePath);

            // меняем ShiftUnlocker.App (17 символов)
            byte[] target17 = System.Text.Encoding.Unicode.GetBytes("ShiftUnlocker.App");
            byte[] replace17 = System.Text.Encoding.Unicode.GetBytes(randomName);
            ReplacePattern(bytes, target17, replace17);

            // меняем ShiftUnlocker (13 символов)
            byte[] target13 = System.Text.Encoding.Unicode.GetBytes("ShiftUnlocker");
            byte[] replace13 = System.Text.Encoding.Unicode.GetBytes(randomName[..13]);
            ReplacePattern(bytes, target13, replace13);

            File.WriteAllBytes(exePath, bytes);
        }
        catch
        {
            // не пропатчилось — запускаем так
        }
    }

    private static void ReplacePattern(byte[] source, byte[] target, byte[] replacement)
    {
        if (target.Length != replacement.Length) return;

        for (int i = 0; i <= source.Length - target.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < target.Length; j++)
            {
                if (source[i + j] != target[j])
                {
                    match = false;
                    break;
                }
            }
            if (match)
            {
                Array.Copy(replacement, 0, source, i, target.Length);
                i += target.Length - 1;
            }
        }
    }

    public static void ScheduleSelfDelete(string exePath)
    {
        try
        {
            if (File.Exists(exePath))
            {
                // отложенное удаление временного exe через cmd
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c timeout /t 2 /nobreak & del /f /q \"{exePath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
            }
        }
        catch
        {
            // не удалилось — не страшно
        }
    }
}
