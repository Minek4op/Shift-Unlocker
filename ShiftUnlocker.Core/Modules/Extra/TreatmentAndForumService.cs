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
    public async Task<Result> CreateForumPackageZipAsync(ITargetContext ctx, BackupManager bm, string zipPath, string? fixlistText = null)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "SU_ForumPkg_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var reportSvc = new SystemReportService();
            string frstReport = await reportSvc.GenerateFrstStyleReportAsync(ctx, bm, sanitizeUsernames: true);
            File.WriteAllText(Path.Combine(tempDir, "FRST_Sanitized_Report.txt"), frstReport);

            var logs = bm.ActionLogs.Select(l => $"[{l.Timestamp:HH:mm:ss}] {(l.Success ? "OK" : "ERR")} [{l.Category}] {l.Description}");
            File.WriteAllLines(Path.Combine(tempDir, "ActionLogs.txt"), logs);

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
