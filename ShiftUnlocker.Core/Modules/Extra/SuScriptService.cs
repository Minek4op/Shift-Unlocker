using System.Diagnostics;
using Microsoft.Win32;
using ShiftUnlocker.Core.Common;
using ShiftUnlocker.Core.Context;
using ShiftUnlocker.Core.Modules.FileManager;
using ShiftUnlocker.Core.Modules.Restrictions;
using ShiftUnlocker.Core.Safety;

namespace ShiftUnlocker.Core.Modules.Extra;

public record SuScriptExecutionResult(int TotalCommands, int Succeeded, int Failed, List<string> LogLines);

public class SuScriptService
{
    public SuScriptExecutionResult ExecuteScript(
        string scriptText,
        ITargetContext ctx,
        BackupManager bm,
        QuarantineManager qm,
        bool dryRun = false)
    {
        var logs = new List<string>();
        int total = 0;
        int ok = 0;
        int fail = 0;

        bool prevDryRun = bm.DryRun;
        bm.DryRun = dryRun;

        try
        {
            var lines = scriptText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#') || line.StartsWith("//") || line.StartsWith(';'))
                    continue;

                int colonIdx = line.IndexOf(':');
                if (colonIdx <= 0)
                {
                    logs.Add($"[SKIP] Неверный синтаксис: {line}");
                    continue;
                }

                total++;
                string cmd = line[..colonIdx].Trim();
                string arg = line[(colonIdx + 1)..].Trim();

                Result res = ExecuteCommand(cmd, arg, ctx, bm, qm, dryRun);
                if (res.Success)
                {
                    ok++;
                    logs.Add($"[OK{(dryRun ? " DRY-RUN" : "")}] {cmd}: {arg}");
                }
                else
                {
                    fail++;
                    logs.Add($"[ERR] {cmd}: {arg} -> {res.ErrorMessage}");
                }
            }
        }
        finally
        {
            bm.DryRun = prevDryRun;
        }

        return new SuScriptExecutionResult(total, ok, fail, logs);
    }

    private static Result ExecuteCommand(
        string cmd,
        string arg,
        ITargetContext ctx,
        BackupManager bm,
        QuarantineManager qm,
        bool dryRun)
    {
        switch (cmd.ToLowerInvariant())
        {
            case "regdelete":
            {
                var parts = arg.Split('|', StringSplitOptions.TrimEntries);
                if (!TrySplitHivePath(parts[0], out string hive, out string subKey))
                    return Result.Fail("Неверный путь реестра (ожидается HKLM\\... или HKCU\\...)");

                if (parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[1]))
                    return bm.BackupAndDeleteRegistryValue(ctx, hive, subKey, parts[1], $"SU Script RegDelete {parts[0]}\\{parts[1]}");

                return bm.BackupAndDeleteSubKeyTree(ctx, hive, subKey, $"SU Script RegDeleteKey {parts[0]}");
            }

            case "regset":
            {
                var parts = arg.Split('|', StringSplitOptions.TrimEntries);
                if (parts.Length < 4) return Result.Fail("Формат: RegSet: HIVE\\Key | ValueName | DWord|String | Value");
                if (!TrySplitHivePath(parts[0], out string hive, out string subKey))
                    return Result.Fail("Неверный путь реестра");

                var kind = parts[2].Equals("String", StringComparison.OrdinalIgnoreCase)
                    ? RegistryValueKind.String
                    : RegistryValueKind.DWord;
                object val = kind == RegistryValueKind.DWord && int.TryParse(parts[3], out int iVal) ? iVal : parts[3];

                return bm.BackupAndSetRegistryValue(ctx, hive, subKey, parts[1], val, kind, $"SU Script RegSet {parts[0]}\\{parts[1]}");
            }

            case "filequarantine":
            {
                if (dryRun) return Result.Ok();
                return qm.QuarantineFile(arg, "SU Script FileQuarantine");
            }

            case "filedelete":
            {
                if (dryRun) return File.Exists(arg) ? Result.Ok() : Result.Fail("Файл не найден");
                return new FileManagerService().DeleteLockedFile(arg, bm);
            }

            case "servicedisable":
            {
                string svcKey = $@"SYSTEM\CurrentControlSet\Services\{arg}";
                return bm.BackupAndSetRegistryValue(ctx, "HKLM", svcKey, "Start", 4, RegistryValueKind.DWord, $"SU Script ServiceDisable {arg}");
            }

            case "taskdisable":
            {
                string taskFile = Path.IsPathRooted(arg)
                    ? arg
                    : Path.Combine(ctx.System32Directory, "Tasks", arg.TrimStart('\\'));
                if (!File.Exists(taskFile)) return Result.Fail("Файл задачи не найден");
                if (dryRun) return Result.Ok();
                var backupRes = bm.BackupFile(taskFile);
                if (!backupRes.Success) return backupRes;
                string disabledPath = taskFile + ".disabled";
                File.Move(taskFile, disabledPath, true);
                bm.RegisterUndo($"Restore task {taskFile}", () =>
                {
                    if (File.Exists(disabledPath))
                    {
                        File.Move(disabledPath, taskFile, true);
                    }
                    return Result.Ok();
                });
                bm.Log("SUScript", $"Task disabled: {taskFile}");
                return Result.Ok();
            }

            case "processkill":
            {
                if (dryRun) return Result.Ok();
                if (int.TryParse(arg, out int pid))
                {
                    using var p = Process.GetProcessById(pid);
                    p.Kill(true);
                    return Result.Ok();
                }

                string name = Path.GetFileNameWithoutExtension(arg);
                var procs = Process.GetProcessesByName(name);
                if (procs.Length == 0) return Result.Fail($"Процесс '{name}' не найден");
                foreach (var p in procs)
                {
                    try { p.Kill(true); }
                    finally { p.Dispose(); }
                }
                return Result.Ok();
            }

            case "hostsreset":
                return new HostsService().ResetHostsToDefault(ctx, bm);

            case "proxyreset":
                return new AdvancedUnlockerService().ResetProxySettings(ctx, bm);

            default:
                return Result.Fail($"Неизвестная команда: {cmd}");
        }
    }

    private static bool TrySplitHivePath(string full, out string hive, out string subKey)
    {
        hive = "";
        subKey = "";
        int idx = full.IndexOf('\\');
        if (idx <= 0) return false;
        hive = full[..idx].Trim().ToUpperInvariant() switch
        {
            "HKEY_LOCAL_MACHINE" => "HKLM",
            "HKEY_CURRENT_USER" => "HKCU",
            "HKEY_CLASSES_ROOT" => "HKCR",
            "HKEY_USERS" => "HKU",
            var h => h
        };
        subKey = full[(idx + 1)..].Trim();
        return hive is "HKLM" or "HKCU" or "HKCR" or "HKU";
    }
}
