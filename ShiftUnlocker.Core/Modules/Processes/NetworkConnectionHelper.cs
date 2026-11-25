using System.Net;
using System.Runtime.InteropServices;
using ShiftUnlocker.Native;

namespace ShiftUnlocker.Core.Modules.Processes;

public static class NetworkConnectionHelper
{
    public static List<NetworkConnectionItem> GetConnections(Dictionary<int, string> pidToNameMap)
    {
        var list = new List<NetworkConnectionItem>();
        list.AddRange(GetTcpConnections(pidToNameMap));
        list.AddRange(GetUdpConnections(pidToNameMap));
        return list;
    }

    private static List<NetworkConnectionItem> GetTcpConnections(Dictionary<int, string> pidToNameMap)
    {
        var list = new List<NetworkConnectionItem>();
        int bufferSize = 0;
        Iphlpapi.GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, true, Iphlpapi.AF_INET, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL);

        IntPtr tcpTablePtr = Marshal.AllocHGlobal(bufferSize);
        try
        {
            if (Iphlpapi.GetExtendedTcpTable(tcpTablePtr, ref bufferSize, true, Iphlpapi.AF_INET, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL) == 0)
            {
                int numEntries = Marshal.ReadInt32(tcpTablePtr);
                IntPtr rowPtr = IntPtr.Add(tcpTablePtr, 4);
                int rowSize = Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();

                for (int i = 0; i < numEntries; i++)
                {
                    var row = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(rowPtr);
                    int pid = (int)row.owningPid;
                    string procName = pidToNameMap.TryGetValue(pid, out var pName) ? pName : (pid == 0 ? "System Idle" : pid == 4 ? "System" : $"PID:{pid}");

                    string stateStr = row.state switch
                    {
                        1 => "CLOSED",
                        2 => "LISTENING",
                        3 => "SYN_SENT",
                        4 => "SYN_RCVD",
                        5 => "ESTABLISHED",
                        6 => "FIN_WAIT1",
                        7 => "FIN_WAIT2",
                        8 => "CLOSE_WAIT",
                        9 => "CLOSING",
                        10 => "LAST_ACK",
                        11 => "TIME_WAIT",
                        12 => "DELETE_TCB",
                        _ => $"State_{row.state}"
                    };

                    list.Add(new NetworkConnectionItem(
                        "TCP",
                        new IPAddress(row.localAddr).ToString(),
                        row.LocalPort,
                        new IPAddress(row.remoteAddr).ToString(),
                        row.RemotePort,
                        stateStr,
                        pid,
                        procName));

                    rowPtr = IntPtr.Add(rowPtr, rowSize);
                }
            }
        }
        catch
        {
            // глушим ошибку
        }
        finally
        {
            Marshal.FreeHGlobal(tcpTablePtr);
        }

        return list;
    }

    private static List<NetworkConnectionItem> GetUdpConnections(Dictionary<int, string> pidToNameMap)
    {
        var list = new List<NetworkConnectionItem>();
        int bufferSize = 0;
        Iphlpapi.GetExtendedUdpTable(IntPtr.Zero, ref bufferSize, true, Iphlpapi.AF_INET, UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID);

        IntPtr udpTablePtr = Marshal.AllocHGlobal(bufferSize);
        try
        {
            if (Iphlpapi.GetExtendedUdpTable(udpTablePtr, ref bufferSize, true, Iphlpapi.AF_INET, UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID) == 0)
            {
                int numEntries = Marshal.ReadInt32(udpTablePtr);
                IntPtr rowPtr = IntPtr.Add(udpTablePtr, 4);
                int rowSize = Marshal.SizeOf<MIB_UDPROW_OWNER_PID>();

                for (int i = 0; i < numEntries; i++)
                {
                    var row = Marshal.PtrToStructure<MIB_UDPROW_OWNER_PID>(rowPtr);
                    int pid = (int)row.owningPid;
                    string procName = pidToNameMap.TryGetValue(pid, out var pName) ? pName : (pid == 0 ? "System Idle" : pid == 4 ? "System" : $"PID:{pid}");

                    list.Add(new NetworkConnectionItem(
                        "UDP",
                        new IPAddress(row.localAddr).ToString(),
                        row.LocalPort,
                        "*",
                        0,
                        "LISTENING",
                        pid,
                        procName));

                    rowPtr = IntPtr.Add(rowPtr, rowSize);
                }
            }
        }
        catch
        {
            // глушим ошибку
        }
        finally
        {
            Marshal.FreeHGlobal(udpTablePtr);
        }

        return list;
    }
}
