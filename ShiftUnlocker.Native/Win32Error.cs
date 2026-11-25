using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ShiftUnlocker.Native;

public static class Win32Error
{
    public const int ERROR_SUCCESS = 0;
    public const int ERROR_ACCESS_DENIED = 5;
    public const int ERROR_NOT_ALL_ASSIGNED = 1300;
    public const int ERROR_PRIVILEGE_NOT_HELD = 1314;
    public const int ERROR_ALREADY_EXISTS = 183;
    public const int ERROR_FILE_NOT_FOUND = 2;
    public const int ERROR_PATH_NOT_FOUND = 3;
    public const int ERROR_SHARING_VIOLATION = 32;

    public static string GetMessage(int errorCode)
    {
        return new Win32Exception(errorCode).Message;
    }
}
