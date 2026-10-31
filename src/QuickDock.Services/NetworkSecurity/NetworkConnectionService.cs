using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;

namespace QuickDock.Services.NetworkSecurity;

public sealed class NetworkConnectionService : INetworkConnectionService
{
    private readonly ILogger<NetworkConnectionService> _logger;

    public NetworkConnectionService(ILogger<NetworkConnectionService> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<ActiveConnectionInfo>> GetActiveConnectionsAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<ActiveConnectionInfo>>(() =>
        {
            var list = new List<ActiveConnectionInfo>();
            IntPtr bufferPtr = IntPtr.Zero;
            int bufferSize = 0;

            try
            {
                // TCP Table
                uint ret = NativeMethods.GetExtendedTcpTable(
                    IntPtr.Zero,
                    ref bufferSize,
                    true,
                    NativeMethods.AF_INET,
                    NativeMethods.TCP_TABLE_OWNER_PID_ALL,
                    0);

                bufferPtr = Marshal.AllocHGlobal(bufferSize);
                ret = NativeMethods.GetExtendedTcpTable(
                    bufferPtr,
                    ref bufferSize,
                    true,
                    NativeMethods.AF_INET,
                    NativeMethods.TCP_TABLE_OWNER_PID_ALL,
                    0);

                if (ret == 0)
                {
                    int count = Marshal.ReadInt32(bufferPtr);
                    IntPtr rowPtr = IntPtr.Add(bufferPtr, 4);
                    int rowSize = Marshal.SizeOf<NativeMethods.MIB_TCPROW_OWNER_PID>();

                    for (int i = 0; i < count; i++)
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        var row = Marshal.PtrToStructure<NativeMethods.MIB_TCPROW_OWNER_PID>(rowPtr);
                        rowPtr = IntPtr.Add(rowPtr, rowSize);

                        int localPort = (row.localPort1 << 8) | row.localPort2;
                        int remotePort = (row.remotePort1 << 8) | row.remotePort2;
                        var localIp = new IPAddress(row.dwLocalAddr);
                        var remoteIp = new IPAddress(row.dwRemoteAddr);
                        string state = ResolveTcpState(row.dwState);

                        // Only non-listening active connections
                        if (row.dwState != 2) // 2 = LISTEN
                        {
                            string procName = ResolveProcessName(row.dwOwningPid);
                            list.Add(new ActiveConnectionInfo
                            {
                                Protocol = ConnectionProtocol.TCP,
                                LocalAddress = localIp.ToString(),
                                LocalPort = localPort,
                                RemoteAddress = remoteIp.ToString(),
                                RemotePort = remotePort,
                                State = state,
                                ProcessId = row.dwOwningPid,
                                ProcessName = procName
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get active TCP connections.");
            }
            finally
            {
                if (bufferPtr != IntPtr.Zero) Marshal.FreeHGlobal(bufferPtr);
            }

            return list;
        }, cancellationToken);
    }

    public Task<IReadOnlyList<ListeningPortInfo>> GetListeningSocketsAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<ListeningPortInfo>>(() =>
        {
            var list = new List<ListeningPortInfo>();
            IntPtr bufferPtr = IntPtr.Zero;
            int bufferSize = 0;

            try
            {
                // 1. TCP Listening sockets
                uint ret = NativeMethods.GetExtendedTcpTable(
                    IntPtr.Zero,
                    ref bufferSize,
                    true,
                    NativeMethods.AF_INET,
                    NativeMethods.TCP_TABLE_OWNER_PID_ALL,
                    0);

                bufferPtr = Marshal.AllocHGlobal(bufferSize);
                ret = NativeMethods.GetExtendedTcpTable(
                    bufferPtr,
                    ref bufferSize,
                    true,
                    NativeMethods.AF_INET,
                    NativeMethods.TCP_TABLE_OWNER_PID_ALL,
                    0);

                if (ret == 0)
                {
                    int count = Marshal.ReadInt32(bufferPtr);
                    IntPtr rowPtr = IntPtr.Add(bufferPtr, 4);
                    int rowSize = Marshal.SizeOf<NativeMethods.MIB_TCPROW_OWNER_PID>();

                    for (int i = 0; i < count; i++)
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        var row = Marshal.PtrToStructure<NativeMethods.MIB_TCPROW_OWNER_PID>(rowPtr);
                        rowPtr = IntPtr.Add(rowPtr, rowSize);

                        if (row.dwState == 2) // MIB_TCP_STATE_LISTEN
                        {
                            int port = (row.localPort1 << 8) | row.localPort2;
                            var localIp = new IPAddress(row.dwLocalAddr);
                            string procName = ResolveProcessName(row.dwOwningPid, out string procPath);

                            list.Add(new ListeningPortInfo
                            {
                                Port = port,
                                Protocol = "TCP",
                                LocalAddress = localIp.ToString(),
                                ProcessId = row.dwOwningPid,
                                ProcessName = procName,
                                ProcessPath = procPath
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get listening sockets.");
            }
            finally
            {
                if (bufferPtr != IntPtr.Zero) Marshal.FreeHGlobal(bufferPtr);
            }

            // 2. UDP Listening sockets
            bufferSize = 0;
            bufferPtr = IntPtr.Zero;
            try
            {
                uint ret = NativeMethods.GetExtendedUdpTable(
                    IntPtr.Zero,
                    ref bufferSize,
                    true,
                    NativeMethods.AF_INET,
                    NativeMethods.UDP_TABLE_OWNER_PID,
                    0);

                bufferPtr = Marshal.AllocHGlobal(bufferSize);
                ret = NativeMethods.GetExtendedUdpTable(
                    bufferPtr,
                    ref bufferSize,
                    true,
                    NativeMethods.AF_INET,
                    NativeMethods.UDP_TABLE_OWNER_PID,
                    0);

                if (ret == 0)
                {
                    int count = Marshal.ReadInt32(bufferPtr);
                    IntPtr rowPtr = IntPtr.Add(bufferPtr, 4);
                    int rowSize = Marshal.SizeOf<NativeMethods.MIB_UDPROW_OWNER_PID>();

                    for (int i = 0; i < count; i++)
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        var row = Marshal.PtrToStructure<NativeMethods.MIB_UDPROW_OWNER_PID>(rowPtr);
                        rowPtr = IntPtr.Add(rowPtr, rowSize);

                        int port = (row.localPort1 << 8) | row.localPort2;
                        var localIp = new IPAddress(row.dwLocalAddr);
                        string procName = ResolveProcessName(row.dwOwningPid, out string procPath);

                        list.Add(new ListeningPortInfo
                        {
                            Port = port,
                            Protocol = "UDP",
                            LocalAddress = localIp.ToString(),
                            ProcessId = row.dwOwningPid,
                            ProcessName = procName,
                            ProcessPath = procPath
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get UDP table.");
            }
            finally
            {
                if (bufferPtr != IntPtr.Zero) Marshal.FreeHGlobal(bufferPtr);
            }

            list.Sort((a, b) => a.Port.CompareTo(b.Port));
            return list;
        }, cancellationToken);
    }

    private static string ResolveProcessName(int pid)
    {
        return ResolveProcessName(pid, out _);
    }

    private static string ResolveProcessName(int pid, out string path)
    {
        path = string.Empty;
        if (pid <= 0) return "System";

        try
        {
            using var proc = Process.GetProcessById(pid);
            try
            {
                path = proc.MainModule?.FileName ?? string.Empty;
            }
            catch
            {
                // access denied
            }
            return proc.ProcessName;
        }
        catch
        {
            return "Exited";
        }
    }

    private static string ResolveTcpState(uint state) => state switch
    {
        1 => "CLOSED",
        2 => "LISTEN",
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
        _ => $"STATE_{state}"
    };

    private static class NativeMethods
    {
        public const int AF_INET = 2;
        public const int TCP_TABLE_OWNER_PID_ALL = 5;
        public const int UDP_TABLE_OWNER_PID = 1;

        [StructLayout(LayoutKind.Sequential)]
        public struct MIB_TCPROW_OWNER_PID
        {
            public uint dwState;
            public uint dwLocalAddr;
            public byte localPort1;
            public byte localPort2;
            public byte localPort3;
            public byte localPort4;
            public uint dwRemoteAddr;
            public byte remotePort1;
            public byte remotePort2;
            public byte remotePort3;
            public byte remotePort4;
            public int dwOwningPid;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MIB_UDPROW_OWNER_PID
        {
            public uint dwLocalAddr;
            public byte localPort1;
            public byte localPort2;
            public byte localPort3;
            public byte localPort4;
            public int dwOwningPid;
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        public static extern uint GetExtendedTcpTable(
            IntPtr pTcpTable,
            ref int pdwSize,
            bool bOrder,
            int ulAf,
            int tableClass,
            uint reserved);

        [DllImport("iphlpapi.dll", SetLastError = true)]
        public static extern uint GetExtendedUdpTable(
            IntPtr pUdpTable,
            ref int pdwSize,
            bool bOrder,
            int ulAf,
            int tableClass,
            uint reserved);
    }
}

