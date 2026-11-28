using System.Diagnostics;
using ShiftUnlocker.Core.Common;
using ShiftUnlocker.Core.Context;

namespace ShiftUnlocker.Core.Modules.Extra;

public record CacheFolderPreview(string Name, string FolderPath, long SizeBytes, int FileCount);

public class DiagnosticsCleanupService
{
    public string ReadOfflineEvtx(string evtxPath, int maxEvents = 30)
    {
        if (!File.Exists(evtxPath)) return "Файл журнала .evtx не найден.";
        return RunCmd("wevtutil.exe", $"qe \"{evtxPath}\" /lf:true /c:{maxEvents} /rd:true /f:text");
    }

    public string GetNetworkDiagnostics()
    {
        return "=== DNS CACHE ===\r\n" + RunCmd("ipconfig.exe", "/displaydns") +
               "\r\n\r\n=== ARP TABLE ===\r\n" + RunCmd("arp.exe", "-a") +
               "\r\n\r\n=== ROUTE TABLE ===\r\n" + RunCmd("route.exe", "print");
    }

    public Result ResetNetworkStack()
    {
        try
        {
            RunCmd("ipconfig.exe", "/flushdns");
            RunCmd("netsh.exe", "winsock reset");
            RunCmd("netsh.exe", "int ip reset");
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    public List<CacheFolderPreview> GetTempCachePreview(ITargetContext ctx)
    {
        var dirs = new (string Name, string Path)[]
        {
            ("User Temp", Path.GetTempPath()),
            ("Windows Temp", Path.Combine(ctx.WindowsDirectory, "Temp")),
            ("Prefetch", Path.Combine(ctx.WindowsDirectory, "Prefetch")),
            ("Windows Update Cache", Path.Combine(ctx.WindowsDirectory, @"SoftwareDistribution\Download"))
        };

        var result = new List<CacheFolderPreview>();
        foreach (var (name, path) in dirs)
        {
            long bytes = 0;
            int count = 0;
            if (Directory.Exists(path))
            {
                try
                {
                    foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly))
                    {
                        try { bytes += new FileInfo(f).Length; count++; } catch { }
                    }
                }
                catch { }
            }
            result.Add(new CacheFolderPreview(name, path, bytes, count));
        }

        return result;
    }

    public (int DeletedFiles, long FreedBytes) CleanTempCaches(IEnumerable<CacheFolderPreview> folders)
    {
        int deleted = 0;
        long freed = 0;
        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder.FolderPath)) continue;
            try
            {
                foreach (var f in Directory.GetFiles(folder.FolderPath, "*", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        long len = new FileInfo(f).Length;
                        File.Delete(f);
                        deleted++;
                        freed += len;
                    }
                    catch { }
                }
            }
            catch { }
        }
        return (deleted, freed);
    }

    private static string RunCmd(string exe, string args)
    {
        try
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
            if (p == null) return "";
            string outStr = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            p.WaitForExit(6000);
            return outStr.Trim();
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
