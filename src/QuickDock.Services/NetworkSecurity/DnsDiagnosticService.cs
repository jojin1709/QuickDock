using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;

namespace QuickDock.Services.NetworkSecurity;

public sealed class DnsDiagnosticService : IDnsDiagnosticService
{
    private readonly ILogger<DnsDiagnosticService> _logger;

    public DnsDiagnosticService(ILogger<DnsDiagnosticService> logger)
    {
        _logger = logger;
    }

    public async Task<DnsQueryResult> QueryDnsAsync(string domain, DnsRecordType recordType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return new DnsQueryResult
            {
                Success = false,
                Domain = domain,
                RecordType = recordType,
                ErrorMessage = "Domain cannot be empty."
            };
        }

        string cleanDomain = domain.Trim().TrimEnd('.');
        // Basic domain name validation
        if (cleanDomain.Length > 255 || cleanDomain.Contains(' ') || cleanDomain.Contains('/') || cleanDomain.Contains('\\'))
        {
            return new DnsQueryResult
            {
                Success = false,
                Domain = cleanDomain,
                RecordType = recordType,
                ErrorMessage = "Invalid domain name syntax."
            };
        }

        var sw = Stopwatch.StartNew();

        try
        {
            var records = new List<DnsRecordItem>();

            // For A / AAAA, we can use Dns.GetHostAddressesAsync as primary or dnsapi.dll
            if (recordType == DnsRecordType.A || recordType == DnsRecordType.AAAA)
            {
                var addresses = await Dns.GetHostAddressesAsync(cleanDomain, cancellationToken);
                foreach (var addr in addresses)
                {
                    if (recordType == DnsRecordType.A && addr.AddressFamily == AddressFamily.InterNetwork)
                    {
                        records.Add(new DnsRecordItem
                        {
                            Name = cleanDomain,
                            RecordType = DnsRecordType.A,
                            Value = addr.ToString(),
                            Ttl = 300
                        });
                    }
                    else if (recordType == DnsRecordType.AAAA && addr.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        records.Add(new DnsRecordItem
                        {
                            Name = cleanDomain,
                            RecordType = DnsRecordType.AAAA,
                            Value = addr.ToString(),
                            Ttl = 300
                        });
                    }
                }
            }
            else if (recordType == DnsRecordType.PTR)
            {
                if (IPAddress.TryParse(cleanDomain, out var ip))
                {
                    var hostEntry = await Dns.GetHostEntryAsync(ip.ToString(), cancellationToken);
                    records.Add(new DnsRecordItem
                    {
                        Name = cleanDomain,
                        RecordType = DnsRecordType.PTR,
                        Value = hostEntry.HostName,
                        Ttl = 300
                    });
                }
                else
                {
                    var hostEntry = await Dns.GetHostEntryAsync(cleanDomain, cancellationToken);
                    records.Add(new DnsRecordItem
                    {
                        Name = cleanDomain,
                        RecordType = DnsRecordType.PTR,
                        Value = hostEntry.HostName,
                        Ttl = 300
                    });
                }
            }
            else
            {
                // Query MX, TXT, NS, CNAME via native Windows dnsapi.dll
                records = QueryWin32Dns(cleanDomain, recordType);
            }

            sw.Stop();

            return new DnsQueryResult
            {
                Success = true,
                Domain = cleanDomain,
                RecordType = recordType,
                Records = records,
                ElapsedMilliseconds = sw.ElapsedMilliseconds,
                ResolverUsed = "Windows System DNS"
            };
        }
        catch (SocketException ex)
        {
            sw.Stop();
            return new DnsQueryResult
            {
                Success = false,
                Domain = cleanDomain,
                RecordType = recordType,
                ElapsedMilliseconds = sw.ElapsedMilliseconds,
                ErrorMessage = $"DNS lookup failed: {ex.Message} ({ex.SocketErrorCode})"
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "DNS query error for {Domain} ({Type})", cleanDomain, recordType);
            return new DnsQueryResult
            {
                Success = false,
                Domain = cleanDomain,
                RecordType = recordType,
                ElapsedMilliseconds = sw.ElapsedMilliseconds,
                ErrorMessage = ex.Message
            };
        }
    }

    private static List<DnsRecordItem> QueryWin32Dns(string domain, DnsRecordType recordType)
    {
        var list = new List<DnsRecordItem>();
        IntPtr pResultSet = IntPtr.Zero;

        try
        {
            int status = NativeMethods.DnsQuery_W(
                domain,
                (ushort)recordType,
                NativeMethods.DNS_QUERY_BYPASS_CACHE,
                IntPtr.Zero,
                out pResultSet,
                IntPtr.Zero);

            if (status != 0 || pResultSet == IntPtr.Zero)
            {
                // Try without bypass cache
                status = NativeMethods.DnsQuery_W(
                    domain,
                    (ushort)recordType,
                    NativeMethods.DNS_QUERY_STANDARD,
                    IntPtr.Zero,
                    out pResultSet,
                    IntPtr.Zero);
            }

            if (status == 0 && pResultSet != IntPtr.Zero)
            {
                IntPtr current = pResultSet;
                while (current != IntPtr.Zero)
                {
                    var record = Marshal.PtrToStructure<NativeMethods.DNS_RECORD>(current);

                    if (record.wType == (ushort)recordType)
                    {
                        string val = string.Empty;
                        int pref = 0;

                        if (recordType == DnsRecordType.MX)
                        {
                            IntPtr pMx = IntPtr.Add(current, Marshal.SizeOf<NativeMethods.DNS_RECORD_HEADER>());
                            var mx = Marshal.PtrToStructure<NativeMethods.DNS_MX_DATA>(pMx);
                            val = Marshal.PtrToStringUni(mx.pNameExchange) ?? "";
                            pref = mx.wPreference;
                        }
                        else if (recordType == DnsRecordType.TXT)
                        {
                            IntPtr pTxt = IntPtr.Add(current, Marshal.SizeOf<NativeMethods.DNS_RECORD_HEADER>());
                            var txt = Marshal.PtrToStructure<NativeMethods.DNS_TXT_DATA>(pTxt);
                            if (txt.pStringArray != IntPtr.Zero && txt.dwStringCount > 0)
                            {
                                val = Marshal.PtrToStringUni(txt.pStringArray) ?? "";
                            }
                        }
                        else if (recordType == DnsRecordType.NS || recordType == DnsRecordType.CNAME)
                        {
                            IntPtr pPtr = IntPtr.Add(current, Marshal.SizeOf<NativeMethods.DNS_RECORD_HEADER>());
                            IntPtr pNameHost = Marshal.ReadIntPtr(pPtr);
                            val = Marshal.PtrToStringUni(pNameHost) ?? "";
                        }

                        if (!string.IsNullOrEmpty(val))
                        {
                            list.Add(new DnsRecordItem
                            {
                                Name = Marshal.PtrToStringUni(record.pName) ?? domain,
                                RecordType = recordType,
                                Value = val,
                                Ttl = (int)record.dwTtl,
                                Preference = pref
                            });
                        }
                    }

                    current = record.pNext;
                }
            }
        }
        finally
        {
            if (pResultSet != IntPtr.Zero)
            {
                NativeMethods.DnsRecordListFree(pResultSet, NativeMethods.DnsFreeRecordList);
            }
        }

        return list;
    }

    private static class NativeMethods
    {
        public const uint DNS_QUERY_STANDARD = 0x0;
        public const uint DNS_QUERY_BYPASS_CACHE = 0x00000008;
        public const int DnsFreeRecordList = 1;

        [StructLayout(LayoutKind.Sequential)]
        public struct DNS_RECORD_HEADER
        {
            public IntPtr pNext;
            public IntPtr pName;
            public ushort wType;
            public ushort wDataLength;
            public uint Flags;
            public uint dwTtl;
            public uint dwReserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DNS_RECORD
        {
            public IntPtr pNext;
            public IntPtr pName;
            public ushort wType;
            public ushort wDataLength;
            public uint Flags;
            public uint dwTtl;
            public uint dwReserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DNS_MX_DATA
        {
            public IntPtr pNameExchange;
            public ushort wPreference;
            public ushort Pad;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DNS_TXT_DATA
        {
            public uint dwStringCount;
            public IntPtr pStringArray;
        }

        [DllImport("dnsapi.dll", EntryPoint = "DnsQuery_W", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int DnsQuery_W(
            string pszName,
            ushort wType,
            uint options,
            IntPtr pExtra,
            out IntPtr ppQueryResultsSet,
            IntPtr pReserved);

        [DllImport("dnsapi.dll")]
        public static extern void DnsRecordListFree(IntPtr pRecordList, int freeType);
    }
}
