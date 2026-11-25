using System.Diagnostics;
using System.Text.RegularExpressions;
using ShiftUnlocker.Core.Common;

namespace ShiftUnlocker.Core.Modules.SystemRecovery;

public record UserAccountInfo(
    string Username,
    string FullName,
    bool IsAdmin,
    bool IsActive,
    bool PasswordRequired,
    string Comment);

public class UserManagerService
{
    public List<UserAccountInfo> GetUsers()
    {
        var list = new List<UserAccountInfo>();
        var usernames = GetUsernamesFromNetUser();

        if (usernames.Count == 0)
        {
            // запасной вариант по папкам в Users
            string usersDir = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\", "Users");
            if (Directory.Exists(usersDir))
            {
                foreach (var dir in Directory.GetDirectories(usersDir))
                {
                    string name = Path.GetFileName(dir);
                    if (!string.Equals(name, "Public", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(name, "Default", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(name, "Default User", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(name, "All Users", StringComparison.OrdinalIgnoreCase))
                    {
                        usernames.Add(name);
                    }
                }
            }
        }

        foreach (var u in usernames)
        {
            var info = GetUserDetails(u);
            list.Add(info);
        }

        return list;
    }

    private static List<string> GetUsernamesFromNetUser()
    {
        var usernames = new List<string>();
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "net.exe",
                Arguments = "user",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.GetEncoding(866)
            };
            using var p = Process.Start(psi);
            if (p != null)
            {
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(3000);

                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                bool startParsing = false;

                foreach (var line in lines)
                {
                    if (line.StartsWith("---"))
                    {
                        startParsing = true;
                        continue;
                    }
                    if (startParsing)
                    {
                        if (line.Contains("успешно") || line.Contains("successfully")) break;
                        var parts = Regex.Split(line.Trim(), @"\s{2,}");
                        foreach (var part in parts)
                        {
                            string trimmed = part.Trim();
                            if (!string.IsNullOrWhiteSpace(trimmed))
                            {
                                usernames.Add(trimmed);
                            }
                        }
                    }
                }
            }
        }
        catch { }

        return usernames;
    }

    private static UserAccountInfo GetUserDetails(string username)
    {
        string fullName = "";
        bool isAdmin = false;
        bool isActive = true;
        bool passwordRequired = false;
        string comment = "";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "net.exe",
                Arguments = $"user \"{username}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.GetEncoding(866)
            };
            using var p = Process.Start(psi);
            if (p != null)
            {
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(3000);

                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    string l = line.Trim();
                    if (l.Contains("Полное имя") || l.Contains("Full Name"))
                    {
                        fullName = ExtractValue(l);
                    }
                    else if (l.Contains("Учетная запись активна") || l.Contains("Account active"))
                    {
                        string val = ExtractValue(l).ToLowerInvariant();
                        isActive = val.Contains("да") || val.Contains("yes");
                    }
                    else if (l.Contains("Требуется пароль") || l.Contains("Password required"))
                    {
                        string val = ExtractValue(l).ToLowerInvariant();
                        passwordRequired = val.Contains("да") || val.Contains("yes");
                    }
                    else if (l.Contains("Комментарий") || l.Contains("Comment"))
                    {
                        comment = ExtractValue(l);
                    }
                    else if (l.Contains("Локальные членства в группах") || l.Contains("Local Group Memberships") || l.Contains("*"))
                    {
                        if (l.Contains("Администраторы") || l.Contains("Administrators"))
                        {
                            isAdmin = true;
                        }
                    }
                }
            }
        }
        catch { }

        return new UserAccountInfo(username, fullName, isAdmin, isActive, passwordRequired, comment);
    }

    private static string ExtractValue(string line)
    {
        int colonIdx = line.IndexOf(':');
        return colonIdx >= 0 && colonIdx < line.Length - 1 ? line.Substring(colonIdx + 1).Trim() : "";
    }

    public Result ResetPassword(string username, string newPassword)
    {
        try
        {
            var (code, err) = RunCommand("net.exe", $"user \"{username}\" \"{newPassword}\"");
            return code == 0 ? Result.Ok() : Result.Fail($"Ошибка сброса пароля: {err}");
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    public Result SetAccountActive(string username, bool active)
    {
        try
        {
            string flag = active ? "yes" : "no";
            var (code, err) = RunCommand("net.exe", $"user \"{username}\" /active:{flag}");
            return code == 0 ? Result.Ok() : Result.Fail($"Ошибка изменения активности: {err}");
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    public Result MakeAdministrator(string username)
    {
        try
        {
            var (code1, err1) = RunCommand("net.exe", $"localgroup \"Администраторы\" \"{username}\" /add");
            if (code1 != 0)
            {
                var (code2, err2) = RunCommand("net.exe", $"localgroup Administrators \"{username}\" /add");
                if (code2 != 0) return Result.Fail($"Ошибка добавления в Администраторы: {err1} / {err2}");
            }
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    public Result CreateUser(string username, string password, bool makeAdmin)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(username)) return Result.Fail("Имя пользователя не может быть пустым.");
            var (code, err) = RunCommand("net.exe", $"user \"{username}\" \"{password}\" /add");
            if (code != 0) return Result.Fail($"Ошибка создания пользователя: {err}");

            if (makeAdmin)
            {
                MakeAdministrator(username);
            }
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    public Result DeleteUser(string username)
    {
        try
        {
            var (code, err) = RunCommand("net.exe", $"user \"{username}\" /delete");
            return code == 0 ? Result.Ok() : Result.Fail($"Ошибка удаления пользователя: {err}");
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    private static (int ExitCode, string Output) RunCommand(string exe, string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.GetEncoding(866)
        };
        using var p = Process.Start(psi);
        if (p == null) return (-1, "Не удалось запустить процесс.");
        string outText = p.StandardOutput.ReadToEnd();
        string errText = p.StandardError.ReadToEnd();
        p.WaitForExit(5000);
        string combined = (outText + " " + errText).Trim();
        return (p.ExitCode, combined);
    }
}
