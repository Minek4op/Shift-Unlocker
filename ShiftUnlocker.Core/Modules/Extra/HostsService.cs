using System.Text;
using ShiftUnlocker.Core.Common;
using ShiftUnlocker.Core.Context;
using ShiftUnlocker.Core.Safety;

namespace ShiftUnlocker.Core.Modules.Extra;

public record HostsEntry(string Ip, string Hostname, bool IsCommented);

public class HostsService
{
    public const string DefaultHostsContent =
@"# Copyright (c) 1993-2009 Microsoft Corp.
#
# This is a sample HOSTS file used by Microsoft TCP/IP for Windows.
#
# localhost name resolution is handled within DNS itself.
#	127.0.0.1       localhost
#	::1             localhost
";

    public string GetHostsFilePath(ITargetContext ctx)
    {
        return Path.Combine(ctx.System32Directory, @"drivers\etc\hosts");
    }

    public string ReadHosts(ITargetContext ctx)
    {
        string path = GetHostsFilePath(ctx);
        if (!File.Exists(path)) return "";
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            return $"Error reading hosts: {ex.Message}";
        }
    }

    public Result ResetHostsToDefault(ITargetContext ctx, BackupManager bm)
    {
        string path = GetHostsFilePath(ctx);
        if (bm.DryRun)
        {
            bm.Log("Hosts", $"[DRY-RUN] Сброс hosts: {path}");
            return Result.Ok();
        }
        bm.BackupFile(path);

        try
        {
            File.SetAttributes(path, FileAttributes.Normal);
            File.WriteAllText(path, DefaultHostsContent, Encoding.ASCII);
            bm.Log("Hosts", "Файл hosts сброшен к чистому стандартному виду");
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail($"Ошибка записи hosts: {ex.Message}");
        }
    }

    public List<string> CheckSuspiciousHostsEntries(ITargetContext ctx)
    {
        var suspicious = new List<string>();
        string content = ReadHosts(ctx);
        using var reader = new StringReader(content);
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#')) continue;

            string lower = line.ToLowerInvariant();
            if (lower.Contains("microsoft") || lower.Contains("windowsupdate") || lower.Contains("update") ||
                lower.Contains("antivirus") || lower.Contains("kaspersky") || lower.Contains("drweb") ||
                lower.Contains("eset") || lower.Contains("symantec") || lower.Contains("sophos") ||
                lower.Contains("virustotal") || lower.Contains("malware"))
            {
                suspicious.Add($"Блокировка или подмена обновлений/защиты: {line}");
            }
        }

        return suspicious;
    }
}
