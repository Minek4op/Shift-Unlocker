using System.Runtime.InteropServices;

namespace ShiftUnlocker.Native;

public enum TCP_TABLE_CLASS
{
    TCP_TABLE_BASIC_LISTENER,
    TCP_TABLE_BASIC_CONNECTIONS,
    TCP_TABLE_BASIC_ALL,
    TCP_TABLE_OWNER_PID_LISTENER,
    TCP_TABLE_OWNER_PID_CONNECTIONS,
    TCP_TABLE_OWNER_PID_ALL,
    TCP_TABLE_OWNER_MODULE_LISTENER,
    TCP_TABLE_OWNER_MODULE_CONNECTIONS,
    TCP_TABLE_OWNER_MODULE_ALL
}

public enum UDP_TABLE_CLASS
{
    UDP_TABLE_BASIC,
    UDP_TABLE_OWNER_PID,
    UDP_TABLE_OWNER_MODULE
}

[StructLayout(LayoutKind.Sequential)]
public struct MIB_TCPROW_OWNER_PID
{
    public uint state;
    public uint localAddr;
    public byte localPort1;
    public byte localPort2;
    public byte localPort3;
    public byte localPort4;
    public uint remoteAddr;
    public byte remotePort1;
    public byte remotePort2;
    public byte remotePort3;
    public byte remotePort4;
    public uint owningPid;

    public ushort LocalPort => (ushort)((localPort1 << 8) | localPort2);
    public ushort RemotePort => (ushort)((remotePort1 << 8) | remotePort2);
}

[StructLayout(LayoutKind.Sequential)]
public struct MIB_UDPROW_OWNER_PID
{
    public uint localAddr;
    public byte localPort1;
    public byte localPort2;
    public byte localPort3;
    public byte localPort4;
    public uint owningPid;

    public ushort LocalPort => (ushort)((localPort1 << 8) | localPort2);
}

public static class Iphlpapi
{
    private const string IphlpapiLib = "iphlpapi.dll";

    public const int AF_INET = 2; // IPv4
    public const int AF_INET6 = 23; // IPv6

    [DllImport(IphlpapiLib, SetLastError = true)]
    public static extern uint GetExtendedTcpTable(
        IntPtr pTcpTable,
        ref int pdwSize,
        [MarshalAs(UnmanagedType.Bool)] bool bOrder,
        int ulAf,
        TCP_TABLE_CLASS tableClass,
        uint reserved = 0);

    [DllImport(IphlpapiLib, SetLastError = true)]
    public static extern uint GetExtendedUdpTable(
        IntPtr pUdpTable,
        ref int pdwSize,
        [MarshalAs(UnmanagedType.Bool)] bool bOrder,
        int ulAf,
        UDP_TABLE_CLASS tableClass,
        uint reserved = 0);
}
