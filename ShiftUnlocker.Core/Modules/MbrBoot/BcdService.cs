using System.Diagnostics;
using ShiftUnlocker.Core.Common;
using ShiftUnlocker.Core.Context;

namespace ShiftUnlocker.Core.Modules.MbrBoot;

public class BcdService
{
    public string EnumerateBcd(ITargetContext ctx)
    {
        string args = ctx.IsOnline ? "/enum all" : $"/store \"{FindOfflineBcdStore(ctx)}\" /enum all";
        return RunBcdedit(args).Output;
    }

    public Result SetSafeBoot(ITargetContext ctx, bool enable, bool withNetwork = false)
    {
        string storeArg = ctx.IsOnline ? "" : $"/store \"{FindOfflineBcdStore(ctx)}\" ";
        string args = enable
            ? $"{storeArg}/set {{default}} safeboot {(withNetwork ? "network" : "minimal")}"
            : $"{storeArg}/deletevalue {{default}} safeboot";

        var (code, output) = RunBcdedit(args);
        return code == 0 ? Result.Ok() : Result.Fail(output);
    }

    public Result SetBootStatusPolicyIgnoreAll(ITargetContext ctx)
    {
        string storeArg = ctx.IsOnline ? "" : $"/store \"{FindOfflineBcdStore(ctx)}\" ";
        var (code, output) = RunBcdedit($"{storeArg}/set {{default}} bootstatuspolicy ignoreallfailures");
        return code == 0 ? Result.Ok() : Result.Fail(output);
    }

    public Result RebootToWinRE()
    {
        try
        {
            var (code, output) = RunProcess("reagentc.exe", "/boottore");
            if (code != 0) return Result.Fail($"reagentc /boottore: {output}");
            Process.Start(new ProcessStartInfo { FileName = "shutdown.exe", Arguments = "/r /t 0", UseShellExecute = true });
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    private static string FindOfflineBcdStore(ITargetContext ctx)
    {
        string bootBcd = Path.Combine(ctx.TargetRootPath, @"Boot\BCD");
        if (File.Exists(bootBcd)) return bootBcd;
        string efiBcd = Path.Combine(ctx.TargetRootPath, @"EFI\Microsoft\Boot\BCD");
        return File.Exists(efiBcd) ? efiBcd : bootBcd;
    }

    private static (int Code, string Output) RunBcdedit(string args) => RunProcess("bcdedit.exe", args);

    private static (int Code, string Output) RunProcess(string exe, string args)
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
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p == null) return (-1, "Failed to start process");
            string o = p.StandardOutput.ReadToEnd() + "\n" + p.StandardError.ReadToEnd();
            p.WaitForExit(5000);
            return (p.ExitCode, o.Trim());
        }
        catch (Exception ex)
        {
            return (-1, ex.Message);
        }
    }
}
