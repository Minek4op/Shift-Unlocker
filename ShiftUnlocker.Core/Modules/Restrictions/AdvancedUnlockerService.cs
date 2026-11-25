using Microsoft.Win32;
using ShiftUnlocker.Core.Common;
using ShiftUnlocker.Core.Context;
using ShiftUnlocker.Core.Safety;

namespace ShiftUnlocker.Core.Modules.Restrictions;

public record HijackedExecutableInfo(string ExeName, string DebuggerPath, bool IsHijacked);

public class AdvancedUnlockerService
{
    public static readonly string[] CriticalExecutables =
    [
        "taskmgr.exe",
        "regedit.exe",
        "cmd.exe",
        "powershell.exe",
        "mmc.exe",
        "msconfig.exe",
        "explorer.exe",
        "procexp.exe",
        "procexp64.exe",
        "autoruns.exe",
        "autoruns64.exe",
        "processhacker.exe",
        "SystemInformer.exe",
        "mbam.exe",
        "cureit.exe",
        "kvrt.exe",
        "adwcleaner.exe",
        "tdsskiller.exe"
    ];

    public List<HijackedExecutableInfo> CheckIfeoHijacks(ITargetContext ctx)
    {
        var list = new List<HijackedExecutableInfo>();
        const string ifeoBase = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options";

        foreach (var exe in CriticalExecutables)
        {
            string keyPath = $@"{ifeoBase}\{exe}";
            object? debuggerVal = ctx.GetValue("HKLM", keyPath, "Debugger");
            if (debuggerVal != null && !string.IsNullOrWhiteSpace(debuggerVal.ToString()))
            {
                list.Add(new HijackedExecutableInfo(exe, debuggerVal.ToString()!, true));
            }
        }

        return list;
    }

    public Result FixIfeoHijack(ITargetContext ctx, BackupManager bm, string exeName)
    {
        string keyPath = $@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\{exeName}";
        return bm.BackupAndDeleteRegistryValue(ctx, "HKLM", keyPath, "Debugger", $"Removed IFEO Debugger hijack for {exeName}");
    }

    public (string? Shell, string? Userinit, string? LegalCaption, string? LegalText) CheckWinlogon(ITargetContext ctx)
    {
        const string winlogon = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
        string? shell = ctx.GetValue("HKLM", winlogon, "Shell")?.ToString();
        string? userinit = ctx.GetValue("HKLM", winlogon, "Userinit")?.ToString();
        string? legalCaption = ctx.GetValue("HKLM", winlogon, "LegalNoticeCaption")?.ToString();
        string? legalText = ctx.GetValue("HKLM", winlogon, "LegalNoticeText")?.ToString();

        return (shell, userinit, legalCaption, legalText);
    }

    public Result FixWinlogonShell(ITargetContext ctx, BackupManager bm)
    {
        const string winlogon = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
        return bm.BackupAndSetRegistryValue(ctx, "HKLM", winlogon, "Shell", "explorer.exe", RegistryValueKind.String, "Restored Winlogon Shell to explorer.exe");
    }

    public Result FixWinlogonUserinit(ITargetContext ctx, BackupManager bm)
    {
        const string winlogon = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
        string expected = Path.Combine(ctx.System32Directory, "userinit.exe") + ",";
        return bm.BackupAndSetRegistryValue(ctx, "HKLM", winlogon, "Userinit", expected, RegistryValueKind.String, $"Restored Winlogon Userinit to {expected}");
    }

    public Result ClearLegalNoticeLocker(ITargetContext ctx, BackupManager bm)
    {
        const string winlogon = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
        var r1 = bm.BackupAndSetRegistryValue(ctx, "HKLM", winlogon, "LegalNoticeCaption", "", RegistryValueKind.String, "Cleared LegalNoticeCaption");
        var r2 = bm.BackupAndSetRegistryValue(ctx, "HKLM", winlogon, "LegalNoticeText", "", RegistryValueKind.String, "Cleared LegalNoticeText");
        return r1.Success && r2.Success ? Result.Ok() : Result.Fail("Failed to clear ransom locker notice.");
    }

    public Result RestoreFileAssociations(ITargetContext ctx, BackupManager bm)
    {
        var errors = new List<string>();

        // фикс .exe
        var r1 = bm.BackupAndSetRegistryValue(ctx, "HKCR", ".exe", "", "exefile", RegistryValueKind.String, "Fixed .exe extension association");
        var r2 = bm.BackupAndSetRegistryValue(ctx, "HKCR", @"exefile\shell\open\command", "", "\"%1\" %*", RegistryValueKind.String, "Fixed exefile open command");
        var r3 = bm.BackupAndDeleteSubKeyTree(ctx, "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.exe\UserChoice", "Removed rogue UserChoice for .exe");

        // фикс .bat
        var r4 = bm.BackupAndSetRegistryValue(ctx, "HKCR", ".bat", "", "batfile", RegistryValueKind.String, "Fixed .bat extension association");
        var r5 = bm.BackupAndSetRegistryValue(ctx, "HKCR", @"batfile\shell\open\command", "", "\"%1\" %*", RegistryValueKind.String, "Fixed batfile open command");

        // фикс .cmd
        var r6 = bm.BackupAndSetRegistryValue(ctx, "HKCR", ".cmd", "", "cmdfile", RegistryValueKind.String, "Fixed .cmd extension association");
        var r7 = bm.BackupAndSetRegistryValue(ctx, "HKCR", @"cmdfile\shell\open\command", "", "\"%1\" %*", RegistryValueKind.String, "Fixed cmdfile open command");

        // фикс .reg
        var r8 = bm.BackupAndSetRegistryValue(ctx, "HKCR", ".reg", "", "regfile", RegistryValueKind.String, "Fixed .reg extension association");
        var r9 = bm.BackupAndSetRegistryValue(ctx, "HKCR", @"regfile\shell\open\command", "", "regedit.exe \"%1\"", RegistryValueKind.String, "Fixed regfile open command");

        // фикс .lnk
        var r10 = bm.BackupAndSetRegistryValue(ctx, "HKCR", ".lnk", "", "lnkfile", RegistryValueKind.String, "Fixed .lnk extension association");
        var r11 = bm.BackupAndDeleteSubKeyTree(ctx, "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.lnk\UserChoice", "Removed rogue UserChoice for .lnk");

        if (!r1.Success) errors.Add("exe ext");
        if (!r2.Success) errors.Add("exefile command");

        return errors.Count == 0 ? Result.Ok() : Result.Fail("Errors fixing associations: " + string.Join(", ", errors));
    }

    public Result ResetProxySettings(ITargetContext ctx, BackupManager bm)
    {
        const string inetSettings = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";
        var r1 = bm.BackupAndSetRegistryValue(ctx, "HKCU", inetSettings, "ProxyEnable", 0, RegistryValueKind.DWord, "Disabled ProxyEnable");
        var r2 = bm.BackupAndDeleteRegistryValue(ctx, "HKCU", inetSettings, "ProxyServer", "Removed ProxyServer");
        var r3 = bm.BackupAndDeleteRegistryValue(ctx, "HKCU", inetSettings, "AutoConfigURL", "Removed rogue AutoConfigURL (PAC proxy)");

        return r1.Success ? Result.Ok() : Result.Fail("Failed to reset proxy settings.");
    }

    public Result UnhideDirectoryContents(string folderPath, BackupManager bm)
    {
        if (!Directory.Exists(folderPath)) return Result.Fail("Directory not found.");

        int count = 0;
        try
        {
            var dirInfo = new DirectoryInfo(folderPath);
            foreach (var file in dirInfo.GetFiles("*", SearchOption.TopDirectoryOnly))
            {
                if (file.Attributes.HasFlag(FileAttributes.Hidden) || file.Attributes.HasFlag(FileAttributes.System))
                {
                    file.Attributes &= ~FileAttributes.Hidden;
                    file.Attributes &= ~FileAttributes.System;
                    count++;
                }
            }

            foreach (var subDir in dirInfo.GetDirectories("*", SearchOption.TopDirectoryOnly))
            {
                if (subDir.Attributes.HasFlag(FileAttributes.Hidden) || subDir.Attributes.HasFlag(FileAttributes.System))
                {
                    subDir.Attributes &= ~FileAttributes.Hidden;
                    subDir.Attributes &= ~FileAttributes.System;
                    count++;
                }
            }

            bm.Log("FileSystem", $"Unhid {count} files/directories in {folderPath}");
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail($"Failed to unhide directory items: {ex.Message}");
        }
    }
}
