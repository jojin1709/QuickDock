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

namespace QuickDock.Services.Developer;

public sealed class LocalDevelopmentService : ILocalDevelopmentService
{
    private readonly ILogger<LocalDevelopmentService> _logger;

    public LocalDevelopmentService(ILogger<LocalDevelopmentService> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<ListeningPortInfo>> GetListeningPortsAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<ListeningPortInfo>>(() =>
        {
            var results = new List<ListeningPortInfo>();
            IntPtr tcpTablePtr = IntPtr.Zero;
            int bufferSize = 0;

            try
            {
                // Call 1: Get buffer size
                uint ret = NativeMethods.GetExtendedTcpTable(
                    IntPtr.Zero,
                    ref bufferSize,
                    true,
                    NativeMethods.AF_INET,
                    NativeMethods.TCP_TABLE_OWNER_PID_ALL,
                    0);

                tcpTablePtr = Marshal.AllocHGlobal(bufferSize);

                // Call 2: Get actual table
                ret = NativeMethods.GetExtendedTcpTable(
                    tcpTablePtr,
                    ref bufferSize,
                    true,
                    NativeMethods.AF_INET,
                    NativeMethods.TCP_TABLE_OWNER_PID_ALL,
                    0);

                if (ret != 0)
                {
                    _logger.LogWarning("GetExtendedTcpTable returned error code: {Ret}", ret);
                    return results;
                }

                int numEntries = Marshal.ReadInt32(tcpTablePtr);
                IntPtr rowPtr = IntPtr.Add(tcpTablePtr, 4);

                int rowSize = Marshal.SizeOf<NativeMethods.MIB_TCPROW_OWNER_PID>();
                var seenPorts = new HashSet<int>();

                for (int i = 0; i < numEntries; i++)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    var row = Marshal.PtrToStructure<NativeMethods.MIB_TCPROW_OWNER_PID>(rowPtr);
                    rowPtr = IntPtr.Add(rowPtr, rowSize);

                    // Filter only listening ports (state == 2: MIB_TCP_STATE_LISTEN)
                    if (row.dwState == NativeMethods.MIB_TCP_STATE_LISTEN)
                    {
                        int port = (row.localPort1 << 8) | row.localPort2;
                        if (!seenPorts.Add(port)) continue;

                        var ip = new IPAddress(row.dwLocalAddr);
                        int pid = row.dwOwningPid;
                        string procName = "System";
                        string procPath = string.Empty;

                        if (pid > 0)
                        {
                            try
                            {
                                using var proc = Process.GetProcessById(pid);
                                procName = proc.ProcessName;
                                try
                                {
                                    procPath = proc.MainModule?.FileName ?? string.Empty;
                                }
                                catch
                                {
                                    // access denied for elevated processes
                                }
                            }
                            catch
                            {
                                procName = "Exited";
                            }
                        }

                        results.Add(new ListeningPortInfo
                        {
                            Port = port,
                            Protocol = "TCP",
                            LocalAddress = ip.ToString(),
                            ProcessId = pid,
                            ProcessName = procName,
                            ProcessPath = procPath
                        });
                    }
                }

                results.Sort((a, b) => a.Port.CompareTo(b.Port));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to inspect local listening ports.");
            }
            finally
            {
                if (tcpTablePtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(tcpTablePtr);
                }
            }

            return results;
        }, cancellationToken);
    }

    private static class NativeMethods
    {
        public const int AF_INET = 2;
        public const int TCP_TABLE_OWNER_PID_ALL = 5;
        public const uint MIB_TCP_STATE_LISTEN = 2;

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

        [DllImport("iphlpapi.dll", SetLastError = true)]
        public static extern uint GetExtendedTcpTable(
            IntPtr pTcpTable,
            ref int pdwSize,
            bool bOrder,
            int ulAf,
            int tableClass,
            uint reserved);
    }
}

