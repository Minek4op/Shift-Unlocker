using System.ComponentModel;
using System.Runtime.InteropServices;
using ShiftUnlocker.Core.Common;
using ShiftUnlocker.Native;

namespace ShiftUnlocker.Core.Security;

public static class PrivilegeManager
{
    private static readonly string[] RequiredPrivileges =
    [
        PrivilegeConstants.SE_BACKUP_NAME,
        PrivilegeConstants.SE_RESTORE_NAME,
        PrivilegeConstants.SE_TAKE_OWNERSHIP_NAME,
        PrivilegeConstants.SE_DEBUG_NAME,
        PrivilegeConstants.SE_LOAD_DRIVER_NAME,
        PrivilegeConstants.SE_SHUTDOWN_NAME,
        PrivilegeConstants.SE_SECURITY_NAME
    ];

    public static Result EnableAllPrivileges()
    {
        try
        {
            IntPtr hProcess = Kernel32.GetCurrentProcess();
            if (!Advapi32.OpenProcessToken(hProcess, PrivilegeConstants.TOKEN_ADJUST_PRIVILEGES | PrivilegeConstants.TOKEN_QUERY, out IntPtr hToken))
            {
                return Result.Fail($"Failed to open process token: {Win32Error.GetMessage(Marshal.GetLastWin32Error())}");
            }

            try
            {
                var errors = new List<string>();
                foreach (string privilege in RequiredPrivileges)
                {
                    if (!EnablePrivilege(hToken, privilege, out string? error))
                    {
                        errors.Add($"{privilege}: {error}");
                    }
                }

                if (errors.Count > 0)
                {
                    return Result.Fail("Some privileges could not be enabled: " + string.Join("; ", errors));
                }

                return Result.Ok();
            }
            finally
            {
                Kernel32.CloseHandle(hToken);
            }
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    private static bool EnablePrivilege(IntPtr hToken, string privilegeName, out string? error)
    {
        error = null;
        if (!Advapi32.LookupPrivilegeValueW(null, privilegeName, out LUID luid))
        {
            error = Win32Error.GetMessage(Marshal.GetLastWin32Error());
            return false;
        }

        TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES
        {
            PrivilegeCount = 1,
            Luid = luid,
            Attributes = PrivilegeConstants.SE_PRIVILEGE_ENABLED
        };

        if (!Advapi32.AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero))
        {
            error = Win32Error.GetMessage(Marshal.GetLastWin32Error());
            return false;
        }

        int lastError = Marshal.GetLastWin32Error();
        if (lastError == Win32Error.ERROR_NOT_ALL_ASSIGNED)
        {
            error = "Privilege not present in token.";
            return false;
        }

        return true;
    }
}
