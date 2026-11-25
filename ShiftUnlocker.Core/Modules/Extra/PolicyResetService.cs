using ShiftUnlocker.Core.Common;
using ShiftUnlocker.Core.Context;
using ShiftUnlocker.Core.Safety;

namespace ShiftUnlocker.Core.Modules.Extra;

public class PolicyResetService
{
    public Result ResetAllPolicies(ITargetContext ctx, BackupManager bm)
    {
        var errors = new List<string>();

        // ветки политик в реестре
        string[] policiesToReset =
        [
            @"Software\Microsoft\Windows\CurrentVersion\Policies\System",
            @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer",
            @"Software\Policies\Microsoft\Windows\System",
            @"Software\Policies\Microsoft\Windows\Explorer"
        ];

        foreach (var p in policiesToReset)
        {
            var r1 = bm.BackupAndDeleteSubKeyTree(ctx, "HKCU", p, $"Сброс политик HKCU: {p}");
            var r2 = bm.BackupAndDeleteSubKeyTree(ctx, "HKLM", p, $"Сброс политик HKLM: {p}");
            if (!r1.Success) errors.Add($"HKCU {p}: {r1.ErrorMessage}");
            if (!r2.Success) errors.Add($"HKLM {p}: {r2.ErrorMessage}");
        }

        // чистим локальные папки GroupPolicy
        string gpDir = Path.Combine(ctx.System32Directory, "GroupPolicy");
        if (Directory.Exists(gpDir))
        {
            try
            {
                File.SetAttributes(gpDir, FileAttributes.Normal);
                Directory.Delete(gpDir, true);
                bm.Log("Policies", "Удалена папка GroupPolicy со сторонними политиками");
            }
            catch (Exception ex)
            {
                errors.Add($"GroupPolicy folder: {ex.Message}");
            }
        }

        string gpuDir = Path.Combine(ctx.System32Directory, "GroupPolicyUsers");
        if (Directory.Exists(gpuDir))
        {
            try
            {
                File.SetAttributes(gpuDir, FileAttributes.Normal);
                Directory.Delete(gpuDir, true);
                bm.Log("Policies", "Удалена папка GroupPolicyUsers со сторонними политиками");
            }
            catch (Exception ex)
            {
                errors.Add($"GroupPolicyUsers folder: {ex.Message}");
            }
        }

        return errors.Count == 0 ? Result.Ok() : Result.Fail("Некоторые политики не удалось сбросить: " + string.Join("; ", errors));
    }
}
