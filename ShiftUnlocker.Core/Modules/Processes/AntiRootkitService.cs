using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using ShiftUnlocker.Core.Context;
using ShiftUnlocker.Native;

namespace ShiftUnlocker.Core.Modules.Processes;

public record AntiRootkitFinding(
    string Category,
    string TargetName,
    string Details,
    string PathOrKey,
    bool IsCritical);

public class AntiRootkitService
{
    public List<AntiRootkitFinding> RunCrossViewScan(ITargetContext ctx)
    {
        var findings = new List<AntiRootkitFinding>();

        if (ctx.IsOnline)
        {
            // 1. Cross-view процессы: Process.GetProcesses vs CreateToolhelp32Snapshot
            var managedPids = new HashSet<int>();
            foreach (var p in Process.GetProcesses())
            {
                try { managedPids.Add(p.Id); }
                finally { p.Dispose(); }
            }

            var toolhelpPids = GetToolhelpPids();
            foreach (var pid in toolhelpPids.Except(managedPids))
            {
                findings.Add(new AntiRootkitFinding(
                    "Скрытый процесс (Cross-View)",
                    $"PID {pid}",
                    "Процесс виден в Toolhelp32Snapshot, но отсутствует в Process.GetProcesses()",
                    $"PID:{pid}",
                    true));
            }

            // 2. Модули в памяти, отсутствующие на диске
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    foreach (ProcessModule m in p.Modules)
                    {
                        if (!string.IsNullOrWhiteSpace(m.FileName) && !File.Exists(m.FileName))
                        {
                            findings.Add(new AntiRootkitFinding(
                                "Модуль без файла на диске",
                                $"{p.ProcessName} -> {m.ModuleName}",
                                $"Модуль загружен в процесс (PID {p.Id}), но файл отсутствует: {m.FileName}",
                                m.FileName,
                                true));
                        }
                    }
                }
                catch { }
                finally { p.Dispose(); }
            }
        }

        // 3. Драйверы без подписи или с отсутствующим файлом
        ScanKernelDrivers(ctx, findings);

        return findings;
    }

    private static HashSet<int> GetToolhelpPids()
    {
        var pids = new HashSet<int>();
        IntPtr snap = Kernel32.CreateToolhelp32Snapshot(Kernel32.TH32CS_SNAPPROCESS, 0);
        if (snap == IntPtr.Zero || snap == new IntPtr(-1)) return pids;

        try
        {
            var entry = new Kernel32.PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf<Kernel32.PROCESSENTRY32>() };
            if (Kernel32.Process32First(snap, ref entry))
            {
                do
                {
                    pids.Add((int)entry.th32ProcessID);
                } while (Kernel32.Process32Next(snap, ref entry));
            }
        }
        finally
        {
            Kernel32.CloseHandle(snap);
        }

        return pids;
    }

    private static void ScanKernelDrivers(ITargetContext ctx, List<AntiRootkitFinding> findings)
    {
        foreach (var svc in ctx.GetSubKeyNames("HKLM", @"SYSTEM\CurrentControlSet\Services"))
        {
            string sk = $@"SYSTEM\CurrentControlSet\Services\{svc}";
            object? typeObj = ctx.GetValue("HKLM", sk, "Type");
            if (typeObj == null || !int.TryParse(typeObj.ToString(), out int svcType)) continue;
            if (svcType != 1 && svcType != 2) continue; // Kernel or FileSystem driver

            string rawPath = ctx.GetValue("HKLM", sk, "ImagePath")?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(rawPath)) continue;

            string resolved = ResolveDriverPath(rawPath, ctx);
            if (!File.Exists(resolved))
            {
                // Игнорируем стандартные системные драйверы, которые могут быть виртуальными
                if (!resolved.Contains(@"\System32\drivers\", StringComparison.OrdinalIgnoreCase))
                {
                    findings.Add(new AntiRootkitFinding(
                        "Драйвер без файла",
                        svc,
                        $"Запись драйвера в реестре указывает на отсутствующий файл: {resolved}",
                        $@"HKLM\{sk}",
                        true));
                }
                continue;
            }

            if (!IsFileSigned(resolved))
            {
                findings.Add(new AntiRootkitFinding(
                    "Неподписанный драйвер",
                    svc,
                    $"Драйвер не имеет цифровой подписи: {resolved}",
                    resolved,
                    true));
            }
        }
    }

    private static string ResolveDriverPath(string raw, ITargetContext ctx)
    {
        string p = raw.Trim('"', ' ');
        if (p.StartsWith(@"\SystemRoot\", StringComparison.OrdinalIgnoreCase))
            p = Path.Combine(ctx.WindowsDirectory, p[@"\SystemRoot\".Length..]);
        else if (p.StartsWith(@"System32\", StringComparison.OrdinalIgnoreCase))
            p = Path.Combine(ctx.WindowsDirectory, p);
        else if (p.StartsWith(@"\??\", StringComparison.OrdinalIgnoreCase))
            p = p[4..];
        return Environment.ExpandEnvironmentVariables(p);
    }

    private static bool IsFileSigned(string filePath)
    {
        try
        {
            #pragma warning disable SYSLIB0057
            using var cert = X509Certificate.CreateFromSignedFile(filePath);
            #pragma warning restore SYSLIB0057
            return cert != null;
        }
        catch
        {
            return false;
        }
    }
}
