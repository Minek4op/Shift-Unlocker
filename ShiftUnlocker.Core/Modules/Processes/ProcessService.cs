using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using ShiftUnlocker.Core.Common;
using ShiftUnlocker.Core.Context;
using ShiftUnlocker.Core.Safety;
using ShiftUnlocker.Native;

namespace ShiftUnlocker.Core.Modules.Processes;

public class ProcessService
{
    private CancellationTokenSource? _watchdogCts;
    private readonly HashSet<string> _watchdogProcessNames = new(StringComparer.OrdinalIgnoreCase);

    public bool IsWatchdogRunning => _watchdogCts != null && !_watchdogCts.IsCancellationRequested;
    public IReadOnlyCollection<string> WatchdogNames => _watchdogProcessNames;

    public async Task<List<ProcessInfoItem>> GetProcessesAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var list = new List<ProcessInfoItem>();
            var all = Process.GetProcesses();

            foreach (var p in all)
            {
                if (ct.IsCancellationRequested) break;

                string filePath = "";
                long ws = 0;
                int threads = 0;
                int handles = 0;
                int session = 0;
                DateTime? start = null;

                try { ws = p.WorkingSet64; } catch { }
                try { threads = p.Threads.Count; } catch { }
                try { handles = p.HandleCount; } catch { }
                try { session = p.SessionId; } catch { }
                try { start = p.StartTime; } catch { }

                try
                {
                    filePath = p.MainModule?.FileName ?? "";
                }
                catch
                {
                    // к системным процессам доступ закрыт
                }

                var (isSuspicious, reason) = EvaluateProcessSuspicious(p.ProcessName, filePath);

                list.Add(new ProcessInfoItem(
                    p.Id,
                    0, // Parent ID will be filled if NtQuerySystemInfo is called
                    p.ProcessName,
                    filePath,
                    ws,
                    threads,
                    handles,
                    session,
                    start,
                    isSuspicious,
                    reason));
            }

            return list;
        }, ct);
    }

    private static (bool Suspicious, string Reason) EvaluateProcessSuspicious(string name, string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return (false, "");

        string lower = filePath.ToLowerInvariant();
        if (lower.Contains(@"\appdata\local\temp") || lower.Contains(@"\windows\temp"))
        {
            return (true, "Исполняется из временного каталога (Temp)");
        }

        if (lower.Contains("$recycle.bin"))
        {
            return (true, "Исполняется из корзины");
        }

        // подмена системного процесса не из System32
        string lowerName = name.ToLowerInvariant();
        if (lowerName is "svchost" or "csrss" or "lsass" or "smss" or "services" or "wininit" or "winlogon")
        {
            if (!lower.Contains(@"\windows\system32\") && !lower.Contains(@"\windows\syswow64\"))
            {
                return (true, $"Маскировка под системный процесс {name} вне System32!");
            }
        }

        return (false, "");
    }

    public Result KillProcess(int processId)
    {
        try
        {
            using var p = Process.GetProcessById(processId);
            p.Kill();
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail($"Failed to terminate process {processId}: {ex.Message}");
        }
    }

    public Result KillProcessTree(int processId)
    {
        try
        {
            using var p = Process.GetProcessById(processId);
            p.Kill(true); // kills entire process tree in .NET
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail($"Failed to terminate process tree {processId}: {ex.Message}");
        }
    }

    public Result SuspendProcess(int processId)
    {
        IntPtr hProcess = Kernel32.OpenProcess(Kernel32.PROCESS_SUSPEND_RESUME, false, processId);
        if (hProcess == IntPtr.Zero)
        {
            return Result.Fail($"Cannot open process {processId} for suspend: {Win32Error.GetMessage(Marshal.GetLastWin32Error())}");
        }

        try
        {
            int status = Ntdll.NtSuspendProcess(hProcess);
            return status == Ntdll.STATUS_SUCCESS ? Result.Ok() : Result.Fail($"NtSuspendProcess failed with status 0x{status:X8}");
        }
        finally
        {
            Kernel32.CloseHandle(hProcess);
        }
    }

    public Result ResumeProcess(int processId)
    {
        IntPtr hProcess = Kernel32.OpenProcess(Kernel32.PROCESS_SUSPEND_RESUME, false, processId);
        if (hProcess == IntPtr.Zero)
        {
            return Result.Fail($"Cannot open process {processId} for resume: {Win32Error.GetMessage(Marshal.GetLastWin32Error())}");
        }

        try
        {
            int status = Ntdll.NtResumeProcess(hProcess);
            return status == Ntdll.STATUS_SUCCESS ? Result.Ok() : Result.Fail($"NtResumeProcess failed with status 0x{status:X8}");
        }
        finally
        {
            Kernel32.CloseHandle(hProcess);
        }
    }

    public void StartWatchdog(IEnumerable<string> processNames, int intervalMs = 500)
    {
        StopWatchdog();

        _watchdogProcessNames.Clear();
        foreach (var n in processNames)
        {
            string clean = Path.GetFileNameWithoutExtension(n);
            if (!string.IsNullOrWhiteSpace(clean))
            {
                _watchdogProcessNames.Add(clean);
            }
        }

        if (_watchdogProcessNames.Count == 0) return;

        _watchdogCts = new CancellationTokenSource();
        var ct = _watchdogCts.Token;

        Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    foreach (var name in _watchdogProcessNames)
                    {
                        var procs = Process.GetProcessesByName(name);
                        foreach (var p in procs)
                        {
                            try
                            {
                                p.Kill();
                            }
                            catch { }
                            finally
                            {
                                p.Dispose();
                            }
                        }
                    }
                }
                catch { }

                await Task.Delay(intervalMs, ct);
            }
        }, ct);
    }

    public void StopWatchdog()
    {
        _watchdogCts?.Cancel();
        _watchdogCts = null;
    }

    public List<ServiceInfoItem> GetServicesFromRegistry(ITargetContext ctx)
    {
        var list = new List<ServiceInfoItem>();
        const string servicesKey = @"SYSTEM\CurrentControlSet\Services";

        try
        {
            string[] subKeys = ctx.GetSubKeyNames("HKLM", servicesKey);
            foreach (var svcName in subKeys)
            {
                string svcPath = $@"{servicesKey}\{svcName}";
                object? disp = ctx.GetValue("HKLM", svcPath, "DisplayName");
                object? img = ctx.GetValue("HKLM", svcPath, "ImagePath");
                object? start = ctx.GetValue("HKLM", svcPath, "Start");
                object? typeObj = ctx.GetValue("HKLM", svcPath, "Type");

                string imagePath = img?.ToString() ?? "";
                string displayName = disp?.ToString() ?? svcName;
                uint startType = start != null ? Convert.ToUInt32(start) : 3;
                uint sType = typeObj != null ? Convert.ToUInt32(typeObj) : 0;

                bool isDriver = (sType & 1) != 0 || (sType & 2) != 0; // SERVICE_KERNEL_DRIVER or SERVICE_FILE_SYSTEM_DRIVER
                bool isNonMs = !imagePath.Contains(@"\System32\", StringComparison.OrdinalIgnoreCase) &&
                               !imagePath.Contains(@"\SysWOW64\", StringComparison.OrdinalIgnoreCase) &&
                               !imagePath.StartsWith("System32\\", StringComparison.OrdinalIgnoreCase);

                string startDesc = startType switch
                {
                    0 => "Загрузочный (Boot)",
                    1 => "Системный (System)",
                    2 => "Автоматически (Auto)",
                    3 => "Вручную (Manual)",
                    4 => "Отключено (Disabled)",
                    _ => $"Тип_{startType}"
                };

                list.Add(new ServiceInfoItem(
                    svcName,
                    displayName,
                    imagePath,
                    startType,
                    startDesc,
                    isNonMs,
                    isDriver));
            }
        }
        catch
        {
            // не смогли прочитать ключ — идем дальше
        }

        return list;
    }

    public Result ChangeServiceStartType(ITargetContext ctx, BackupManager bm, string serviceName, uint newStartType)
    {
        string svcPath = $@"SYSTEM\CurrentControlSet\Services\{serviceName}";
        return bm.BackupAndSetRegistryValue(
            ctx,
            "HKLM",
            svcPath,
            "Start",
            unchecked((int)newStartType),
            RegistryValueKind.DWord,
            $"Changed start type for service '{serviceName}' to {newStartType}");
    }
}
