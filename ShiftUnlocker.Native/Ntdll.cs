using System.Runtime.InteropServices;

namespace ShiftUnlocker.Native;

public static class Ntdll
{
    private const string NtdllLib = "ntdll.dll";

    public const int STATUS_SUCCESS = 0x00000000;

    [DllImport(NtdllLib, SetLastError = false)]
    public static extern int NtSuspendProcess(IntPtr processHandle);

    [DllImport(NtdllLib, SetLastError = false)]
    public static extern int NtResumeProcess(IntPtr processHandle);

    [DllImport(NtdllLib, SetLastError = false)]
    public static extern int NtQuerySystemInformation(
        int systemInformationClass,
        IntPtr systemInformation,
        int systemInformationLength,
        out int returnLength);
}
