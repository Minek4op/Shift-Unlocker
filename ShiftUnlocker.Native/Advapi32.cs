using System.Runtime.InteropServices;

namespace ShiftUnlocker.Native;

public static class Advapi32
{
    private const string Advapi32Lib = "advapi32.dll";

    public static readonly IntPtr HKEY_LOCAL_MACHINE = new IntPtr(unchecked((int)0x80000002));
    public static readonly IntPtr HKEY_USERS = new IntPtr(unchecked((int)0x80000003));
    public static readonly IntPtr HKEY_CURRENT_USER = new IntPtr(unchecked((int)0x80000001));

    public const int KEY_QUERY_VALUE = 0x0001;
    public const int KEY_SET_VALUE = 0x0002;
    public const int KEY_CREATE_SUB_KEY = 0x0004;
    public const int KEY_ENUMERATE_SUB_KEYS = 0x0008;
    public const int KEY_NOTIFY = 0x0010;
    public const int KEY_CREATE_LINK = 0x0020;
    public const int KEY_READ = 0x20019;
    public const int KEY_WRITE = 0x20006;
    public const int KEY_ALL_ACCESS = 0xF003F;

    [DllImport(Advapi32Lib, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int RegLoadKeyW(IntPtr hKey, string lpSubKey, string lpFile);

    [DllImport(Advapi32Lib, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int RegUnLoadKeyW(IntPtr hKey, string lpSubKey);

    [DllImport(Advapi32Lib, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int RegOpenKeyExW(IntPtr hKey, string lpSubKey, uint ulOptions, int samDesired, out IntPtr phkResult);

    [DllImport(Advapi32Lib, SetLastError = true)]
    public static extern int RegCloseKey(IntPtr hKey);

    [DllImport(Advapi32Lib, SetLastError = true)]
    public static extern int RegFlushKey(IntPtr hKey);

    [DllImport(Advapi32Lib, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int RegSaveKeyExW(IntPtr hKey, string lpFile, IntPtr lpSecurityAttributes, uint Flags);

    [DllImport(Advapi32Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport(Advapi32Lib, CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool LookupPrivilegeValueW(string? lpSystemName, string lpName, out LUID lpLuid);

    [DllImport(Advapi32Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AdjustTokenPrivileges(
        IntPtr TokenHandle,
        [MarshalAs(UnmanagedType.Bool)] bool DisableAllPrivileges,
        ref TOKEN_PRIVILEGES NewState,
        uint BufferLength,
        IntPtr PreviousState,
        IntPtr ReturnLength);

    // менеджер служб
    public const uint SC_MANAGER_ALL_ACCESS = 0xF003F;
    public const uint SC_MANAGER_CONNECT = 0x0001;
    public const uint SC_MANAGER_ENUMERATE_SERVICE = 0x0004;

    public const uint SERVICE_ALL_ACCESS = 0xF01FF;
    public const uint SERVICE_START = 0x0010;
    public const uint SERVICE_STOP = 0x0020;
    public const uint SERVICE_QUERY_STATUS = 0x0004;
    public const uint SERVICE_CHANGE_CONFIG = 0x0002;

    public const uint SERVICE_CONTROL_STOP = 1;

    public const uint SERVICE_NO_CHANGE = 0xFFFFFFFF;
    public const uint SERVICE_AUTO_START = 2;
    public const uint SERVICE_DEMAND_START = 3;
    public const uint SERVICE_DISABLED = 4;

    [StructLayout(LayoutKind.Sequential)]
    public struct SERVICE_STATUS_PROCESS
    {
        public uint dwServiceType;
        public uint dwCurrentState;
        public uint dwControlsAccepted;
        public uint dwWin32ExitCode;
        public uint dwServiceSpecificExitCode;
        public uint dwCheckPoint;
        public uint dwWaitHint;
        public uint dwProcessId;
        public uint dwServiceFlags;
    }

    [DllImport(Advapi32Lib, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr OpenSCManagerW(string? lpMachineName, string? lpDatabaseName, uint dwDesiredAccess);

    [DllImport(Advapi32Lib, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr OpenServiceW(IntPtr hSCManager, string lpServiceName, uint dwDesiredAccess);

    [DllImport(Advapi32Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseServiceHandle(IntPtr hSCObject);

    [DllImport(Advapi32Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool StartServiceW(IntPtr hService, uint dwNumServiceArgs, string[]? lpServiceArgVectors);

    [DllImport(Advapi32Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ControlService(IntPtr hService, uint dwControl, ref SERVICE_STATUS_PROCESS lpServiceStatus);

    [DllImport(Advapi32Lib, CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ChangeServiceConfigW(
        IntPtr hService,
        uint dwServiceType,
        uint dwStartType,
        uint dwErrorControl,
        string? lpBinaryPathName,
        string? lpLoadOrderGroup,
        IntPtr lpdwTagId,
        string? lpDependencies,
        string? lpServiceStartName,
        string? lpPassword,
        string? lpDisplayName);

    [DllImport(Advapi32Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool QueryServiceStatusEx(
        IntPtr hService,
        int InfoLevel,
        IntPtr lpBuffer,
        uint cbBufSize,
        out uint pcbBytesNeeded);

    // смена владельца и прав
    public const int SE_FILE_OBJECT = 1;
    public const int SE_REGISTRY_KEY = 4;
    public const uint OWNER_SECURITY_INFORMATION = 0x00000001;
    public const uint DACL_SECURITY_INFORMATION = 0x00000004;

    [DllImport(Advapi32Lib, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern uint SetNamedSecurityInfoW(
        string pObjectName,
        int ObjectType,
        uint SecurityInfo,
        IntPtr psidOwner,
        IntPtr psidGroup,
        IntPtr pDacl,
        IntPtr pSacl);

    public const uint REG_NOTIFY_CHANGE_NAME = 0x00000001;
    public const uint REG_NOTIFY_CHANGE_LAST_SET = 0x00000004;

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern int RegNotifyChangeKeyValue(
        IntPtr hKey,
        bool bWatchSubtree,
        uint dwNotifyFilter,
        IntPtr hEvent,
        bool fAsynchronous);
}
