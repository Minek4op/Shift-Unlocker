using ShiftUnlocker.Core.Common;
using ShiftUnlocker.Core.Context;
using ShiftUnlocker.Core.Modules.QuickScan;
using ShiftUnlocker.Core.Modules.Restrictions;
using ShiftUnlocker.Core.Safety;

namespace ShiftUnlocker.Core.Modules.Extra;

public enum HealingProfile
{
    DiagnosticsOnly,
    SafeHealing,
    FullHealing
}

public class AutoHealWizardService
{
    private readonly QuickScanService _scanService = new();
    private readonly AdvancedUnlockerService _advancedUnlocker = new();
    private readonly RestrictionsService _restrictionsService = new();
    private readonly HostsService _hostsService = new();
    private readonly PolicyResetService _policyReset = new();

    public async Task<Result> ExecuteProfileAsync(
        HealingProfile profile,
        ITargetContext ctx,
        BackupManager bm,
        Action<string>? onProgress = null,
        CancellationToken ct = default)
    {
        onProgress?.Invoke("Запуск сканирования системы...");
        var findings = await _scanService.RunQuickScanAsync(ctx, ct);
        onProgress?.Invoke($"Найдено проблем: {findings.Count}");

        if (profile == HealingProfile.DiagnosticsOnly)
        {
            return Result.Ok();
        }

        int fixedCount = 0;
        int failedCount = 0;

        // сначала чиним критичные находки
        foreach (var finding in findings)
        {
            if (ct.IsCancellationRequested) break;

            if (profile == HealingProfile.SafeHealing && finding.Severity == FindingSeverity.Info)
            {
                continue;
            }

            onProgress?.Invoke($"Исправление: {finding.Title}...");
            var res = finding.FixAction(ctx, bm);
            if (res.Success)
            {
                fixedCount++;
            }
            else
            {
                failedCount++;
            }
        }

        // доп. действия для полного лечения
        if (profile == HealingProfile.FullHealing && !ct.IsCancellationRequested)
        {
            onProgress?.Invoke("Сброс файла hosts к эталонному виду...");
            _hostsService.ResetHostsToDefault(ctx, bm);

            onProgress?.Invoke("Сброс групповых политик...");
            _policyReset.ResetAllPolicies(ctx, bm);

            onProgress?.Invoke("Восстановление ассоциаций файлов...");
            _advancedUnlocker.RestoreFileAssociations(ctx, bm);
        }

        onProgress?.Invoke($"Лечение завершено. Успешно: {fixedCount}, Ошибок: {failedCount}.");
        return failedCount == 0 ? Result.Ok() : Result.Fail($"{failedCount} операций завершились с ошибкой.");
    }
}
