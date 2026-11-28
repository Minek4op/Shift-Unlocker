using System.IO.Compression;
using System.Text.Json;
using ShiftUnlocker.Core.Common;
using ShiftUnlocker.Core.Context;
using ShiftUnlocker.Core.Safety;

namespace ShiftUnlocker.Core.Modules.Extra;

public record TreatmentProfile(string Name, string Description, string SuScript);

public class TreatmentProfileService
{
    public List<TreatmentProfile> GetBuiltInProfiles() =>
    [
        new("Локер с баннером", "Сбрасывает Shell/Userinit, LegalNotice и основные политики блокировки",
            "RegSet: HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Winlogon | Shell | String | explorer.exe\r\n" +
            "RegSet: HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Winlogon | LegalNoticeCaption | String | \r\n" +
            "RegSet: HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Winlogon | LegalNoticeText | String | \r\n" +
            "RegDelete: HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\System | DisableTaskMgr\r\n" +
            "RegDelete: HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\System | DisableRegistryTools"),
        new("Ярлыки на флешке", "Включает показ скрытых файлов, отключает автозапуск со сменных дисков",
            "RegSet: HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced | Hidden | DWord | 1\r\n" +
            "RegSet: HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced | ShowSuperHidden | DWord | 1\r\n" +
            "RegSet: HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced | HideFileExt | DWord | 0\r\n" +
            "RegSet: HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\Explorer | NoDriveTypeAutoRun | DWord | 255"),
        new("Реклама в браузере", "Сбрасывает прокси, PAC-скрипт и файл hosts",
            "HostsReset:\r\nProxyReset:")
    ];

    public Result ExportProfile(TreatmentProfile profile, string filePath)
    {
        try
        {
            File.WriteAllText(filePath, JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }));
            return Result.Ok();
        }
        catch (Exception ex) { return Result.Fail(ex.Message); }
    }

    public TreatmentProfile? ImportProfile(string filePath)
    {
        try
        {
            return JsonSerializer.Deserialize<TreatmentProfile>(File.ReadAllText(filePath));
        }
        catch { return null; }
    }
}

public class ForumPackageService
{
    public async Task<(string ReportText, string LogsText)> BuildPackageContentsAsync(ITargetContext ctx, BackupManager bm)
    {
        var reportSvc = new SystemReportService();
        string frstReport = await reportSvc.GenerateFrstStyleReportAsync(ctx, bm, sanitizeUsernames: true);

        string user = Environment.UserName;
        string machine = Environment.MachineName;
        var logs = bm.ActionLogs.Select(l =>
        {
            string line = $"[{l.Timestamp:HH:mm:ss}] {(l.Success ? "OK" : "ERR")} [{l.Category}] {l.Description}";
            if (!string.IsNullOrEmpty(user)) line = line.Replace(user, "[USER]", StringComparison.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(machine)) line = line.Replace(machine, "[PC]", StringComparison.OrdinalIgnoreCase);
            return line;
        });

        return (frstReport, string.Join(Environment.NewLine, logs));
    }

    public async Task<Result> CreateForumPackageZipAsync(ITargetContext ctx, BackupManager bm, string zipPath, string? fixlistText = null)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "SU_DiagPkg_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var (frstReport, logsText) = await BuildPackageContentsAsync(ctx, bm);
            File.WriteAllText(Path.Combine(tempDir, "Diagnostic_PartialMask_Report.txt"), frstReport);
            File.WriteAllText(Path.Combine(tempDir, "ActionLogs.txt"), logsText);

            if (!string.IsNullOrWhiteSpace(fixlistText))
            {
                File.WriteAllText(Path.Combine(tempDir, "fixlist_su.txt"), fixlistText);
            }

            if (File.Exists(zipPath)) File.Delete(zipPath);
            ZipFile.CreateFromDirectory(tempDir, zipPath);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail($"Ошибка создания архива: {ex.Message}");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}
