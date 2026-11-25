using ShiftUnlocker.Core.Common;
using ShiftUnlocker.Core.Context;
using ShiftUnlocker.Core.Modules.Restrictions;
using ShiftUnlocker.Core.Safety;

namespace ShiftUnlocker.Core.Modules.QuickScan;

public enum FindingSeverity
{
    Info,
    Low,
    Medium,
    High,
    Critical
}

public class ScanFinding
{
    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public FindingSeverity Severity { get; }
    public string Category { get; }
    public Func<ITargetContext, BackupManager, Result> FixAction { get; }

    public ScanFinding(
        string id,
        string title,
        string description,
        FindingSeverity severity,
        string category,
        Func<ITargetContext, BackupManager, Result> fixAction)
    {
        Id = id;
        Title = title;
        Description = description;
        Severity = severity;
        Category = category;
        FixAction = fixAction;
    }
}

public class QuickScanService
{
    private readonly RestrictionsService _restrictionsService = new();
    private readonly AdvancedUnlockerService _advancedUnlocker = new();

    public async Task<List<ScanFinding>> RunQuickScanAsync(ITargetContext ctx, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var findings = new List<ScanFinding>();

            // проверка перехватов IFEO
            var hijacks = _advancedUnlocker.CheckIfeoHijacks(ctx);
            foreach (var h in hijacks)
            {
                findings.Add(new ScanFinding(
                    $"ifeo_{h.ExeName}",
                    $"Перехват запуска: {h.ExeName}",
                    $"Ветка IFEO содержит значение Debugger='{h.DebuggerPath}', блокирующее открытие утилиты.",
                    FindingSeverity.Critical,
                    "Перехват (IFEO)",
                    (c, bm) => _advancedUnlocker.FixIfeoHijack(c, bm, h.ExeName)));
            }

            // проверка Shell и Userinit в Winlogon
            var winlogon = _advancedUnlocker.CheckWinlogon(ctx);
            if (!string.IsNullOrEmpty(winlogon.Shell) &&
                !winlogon.Shell.Trim().Equals("explorer.exe", StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(new ScanFinding(
                    "winlogon_shell",
                    "Подмена системной оболочки (Shell)",
                    $"Вместо стандартного explorer.exe запускается: '{winlogon.Shell}'. Это признак локера или трояна.",
                    FindingSeverity.Critical,
                    "Оболочка",
                    (c, bm) => _advancedUnlocker.FixWinlogonShell(c, bm)));
            }

            if (!string.IsNullOrEmpty(winlogon.Userinit) &&
                !winlogon.Userinit.Contains("userinit.exe", StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(new ScanFinding(
                    "winlogon_userinit",
                    "Подмена инициализатора пользователя (Userinit)",
                    $"Userinit изменен: '{winlogon.Userinit}'. Это мешает входу в систему или запускает вирус при старте.",
                    FindingSeverity.Critical,
                    "Оболочка",
                    (c, bm) => _advancedUnlocker.FixWinlogonUserinit(c, bm)));
            }

            if (!string.IsNullOrWhiteSpace(winlogon.LegalCaption) || !string.IsNullOrWhiteSpace(winlogon.LegalText))
            {
                findings.Add(new ScanFinding(
                    "winlogon_legalnotice",
                    "Экран блокировки (Ransom Notice)",
                    $"Обнаружен текст блокировщика экрана: '{winlogon.LegalCaption} / {winlogon.LegalText}'.",
                    FindingSeverity.High,
                    "Локер",
                    (c, bm) => _advancedUnlocker.ClearLegalNoticeLocker(c, bm)));
            }

            // проверка ассоциаций .exe
            object? exeCmd = ctx.GetValue("HKCR", @"exefile\shell\open\command", "");
            if (exeCmd != null && !exeCmd.ToString()!.Contains("%1"))
            {
                findings.Add(new ScanFinding(
                    "assoc_exe",
                    "Повреждена ассоциация запуска .exe файлов",
                    $"Обработчик exefile изменен: '{exeCmd}'. Программы не смогут запускаться.",
                    FindingSeverity.Critical,
                    "Ассоциации",
                    (c, bm) => _advancedUnlocker.RestoreFileAssociations(c, bm)));
            }

            // проверка прокси
            object? proxyEnable = ctx.GetValue("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Internet Settings", "ProxyEnable");
            object? autoConfigUrl = ctx.GetValue("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Internet Settings", "AutoConfigURL");
            if (proxyEnable != null && Convert.ToInt32(proxyEnable) == 1 || autoConfigUrl != null)
            {
                findings.Add(new ScanFinding(
                    "net_proxy",
                    "Активен прокси-сервер или автоскрипт (PAC)",
                    $"Трафик перенаправляется через сторонний прокси (AutoConfigURL='{autoConfigUrl}').",
                    FindingSeverity.High,
                    "Сеть",
                    (c, bm) => _advancedUnlocker.ResetProxySettings(c, bm)));
            }

            // проверка основных блокировок политик
            var restrictions = _restrictionsService.CheckAll(ctx);
            foreach (var r in restrictions)
            {
                if (r.CurrentStatus == RestrictionStatus.Restricted)
                {
                    FindingSeverity sev = r.Id switch
                    {
                        "tools_taskmgr" or "tools_taskmgr_lm" or "tools_regedit" or "tools_regedit_lm" or "tools_cmd" or "tools_cmd_lm" => FindingSeverity.High,
                        "sec_defender_lm" or "sec_defender_realtime" or "sec_uac_lua" => FindingSeverity.High,
                        "sec_sr_disable" or "storage_usbstor" => FindingSeverity.Medium,
                        _ => FindingSeverity.Low
                    };

                    findings.Add(new ScanFinding(
                        r.Id,
                        r.Title,
                        r.Description + $" (Текущее значение: {r.CurrentRawValue})",
                        sev,
                        r.Category,
                        (c, bm) => r.Unlock(c, bm)));
                }
            }

            return findings.OrderByDescending(f => f.Severity).ToList();
        }, ct);
    }
}
