using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;

namespace QuickDock.Services.NetworkSecurity;

public sealed class RoutingNeighborService : IRoutingNeighborService
{
    private readonly ILogger<RoutingNeighborService> _logger;

    public RoutingNeighborService(ILogger<RoutingNeighborService> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<NetworkRouteInfo>> GetRoutingTableAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<NetworkRouteInfo>>(() =>
        {
            var routes = new List<NetworkRouteInfo>();
            IntPtr bufferPtr = IntPtr.Zero;
            int bufferSize = 0;

            try
            {
                uint ret = NativeMethods.GetIpForwardTable(IntPtr.Zero, ref bufferSize, true);
                if (bufferSize > 0)
                {
                    bufferPtr = Marshal.AllocHGlobal(bufferSize);
                    ret = NativeMethods.GetIpForwardTable(bufferPtr, ref bufferSize, true);

                    if (ret == 0)
                    {
                        int count = Marshal.ReadInt32(bufferPtr);
                        IntPtr rowPtr = IntPtr.Add(bufferPtr, 4);
                        int rowSize = Marshal.SizeOf<NativeMethods.MIB_IPFORWARDROW>();

                        for (int i = 0; i < count; i++)
                        {
                            if (cancellationToken.IsCancellationRequested) break;

                            var row = Marshal.PtrToStructure<NativeMethods.MIB_IPFORWARDROW>(rowPtr);
                            rowPtr = IntPtr.Add(rowPtr, rowSize);

                            var dest = new IPAddress(row.dwForwardDest);
                            var mask = new IPAddress(row.dwForwardMask);
                            var nextHop = new IPAddress(row.dwForwardNextHop);

                            routes.Add(new NetworkRouteInfo
                            {
                                Destination = dest.ToString(),
                                Netmask = mask.ToString(),
                                Gateway = nextHop.ToString(),
                                InterfaceAddress = $"Interface #{row.dwForwardIfIndex}",
                                Metric = (int)row.dwForwardMetric1
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read IP forward table.");
            }
            finally
            {
                if (bufferPtr != IntPtr.Zero) Marshal.FreeHGlobal(bufferPtr);
            }

            return routes;
        }, cancellationToken);
    }

    public Task<IReadOnlyList<NeighborCacheInfo>> GetNeighborCacheAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<NeighborCacheInfo>>(() =>
        {
            var neighbors = new List<NeighborCacheInfo>();
            IntPtr bufferPtr = IntPtr.Zero;
            int bufferSize = 0;

            try
            {
                uint ret = NativeMethods.GetIpNetTable(IntPtr.Zero, ref bufferSize, true);
                if (bufferSize > 0)
                {
                    bufferPtr = Marshal.AllocHGlobal(bufferSize);
                    ret = NativeMethods.GetIpNetTable(bufferPtr, ref bufferSize, true);

                    if (ret == 0)
                    {
                        int count = Marshal.ReadInt32(bufferPtr);
                        IntPtr rowPtr = IntPtr.Add(bufferPtr, 4);
                        int rowSize = Marshal.SizeOf<NativeMethods.MIB_IPNETROW>();

                        for (int i = 0; i < count; i++)
                        {
                            if (cancellationToken.IsCancellationRequested) break;

                            var row = Marshal.PtrToStructure<NativeMethods.MIB_IPNETROW>(rowPtr);
                            rowPtr = IntPtr.Add(rowPtr, rowSize);

                            var ip = new IPAddress(row.dwAddr);
                            string mac = FormatMac(row.bPhysAddr, (int)row.dwPhysAddrLen);
                            string state = row.dwType switch
                            {
                                4 => "Static",
                                3 => "Dynamic",
                                2 => "Invalid",
                                _ => "Other"
                            };

                            neighbors.Add(new NeighborCacheInfo
                            {
                                IpAddress = ip.ToString(),
                                MacAddress = mac,
                                State = state,
                                InterfaceIndex = (int)row.dwIndex,
                                InterfaceName = $"Interface #{row.dwIndex}"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read ARP neighbor cache.");
            }
            finally
            {
                if (bufferPtr != IntPtr.Zero) Marshal.FreeHGlobal(bufferPtr);
            }

            return neighbors;
        }, cancellationToken);
    }

    private static string FormatMac(byte[] bPhysAddr, int len)
    {
        if (bPhysAddr == null || len <= 0) return string.Empty;
        int safeLen = Math.Min(len, bPhysAddr.Length);
        var bytes = new byte[safeLen];
        Array.Copy(bPhysAddr, bytes, safeLen);
        return string.Join("-", Array.ConvertAll(bytes, b => b.ToString("X2")));
    }

    private static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct MIB_IPFORWARDROW
        {
            public uint dwForwardDest;
            public uint dwForwardMask;
            public uint dwForwardPolicy;
            public uint dwForwardNextHop;
            public uint dwForwardIfIndex;
            public uint dwForwardType;
            public uint dwForwardProto;
            public uint dwForwardAge;
            public uint dwForwardNextHopAS;
            public uint dwForwardMetric1;
            public uint dwForwardMetric2;
            public uint dwForwardMetric3;
            public uint dwForwardMetric4;
            public uint dwForwardMetric5;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MIB_IPNETROW
        {
            public uint dwIndex;
            public uint dwPhysAddrLen;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] bPhysAddr;
            public uint dwAddr;
            public uint dwType;
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        public static extern uint GetIpForwardTable(IntPtr pIpForwardTable, ref int pdwSize, bool bOrder);

        [DllImport("iphlpapi.dll", SetLastError = true)]
        public static extern uint GetIpNetTable(IntPtr pIpNetTable, ref int pdwSize, bool bOrder);
    }
}
