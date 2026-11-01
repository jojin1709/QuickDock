using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;
using Windows.ApplicationModel.DataTransfer;

namespace QuickDock.App.ViewModels;

public sealed partial class NetworkSecurityViewModel : ObservableObject
{
    private readonly INetworkOverviewService _networkService;
    private readonly IDnsDiagnosticService _dnsService;
    private readonly IConnectivityService _connectivityService;
    private readonly INetworkConnectionService _connectionService;
    private readonly IRoutingNeighborService _routingService;
    private readonly ITlsInspectionService _tlsService;
    private readonly IHttpSecurityService _httpSecurityService;
    private readonly IIpDomainService _ipDomainService;
    private readonly IFirewallStatusService _firewallService;
    private readonly IWindowsSecurityService _windowsSecurityService;
    private readonly ILocalSecurityCheckService _securityCheckService;
    private readonly IFileIntegrityService _fileIntegrityService;
    private readonly ISecurityReportService _reportService;
    private readonly ILogger<NetworkSecurityViewModel> _logger;

    private CancellationTokenSource? _pingCts;

    [ObservableProperty]
    private string _selectedSection = "Overview"; // Overview, Adapters, Dns, Connectivity, Connections, Tls, Headers, Firewall, SecurityChecks, Integrity, Reports

    #region Overview Properties

    [ObservableProperty]
    private NetworkOverviewInfo? _overview;

    [ObservableProperty]
    private bool _isOverviewBusy;

    #endregion

    #region Adapters Properties

    public ObservableCollection<NetworkAdapterDetail> Adapters { get; } = [];

    [ObservableProperty]
    private bool _isAdaptersBusy;

    #endregion

    #region DNS Properties

    [ObservableProperty]
    private string _dnsDomain = "github.com";

    [ObservableProperty]
    private DnsRecordType _selectedDnsRecordType = DnsRecordType.A;

    [ObservableProperty]
    private DnsQueryResult? _dnsResult;

    [ObservableProperty]
    private bool _isDnsBusy;

    #endregion

    #region Connectivity Properties

    [ObservableProperty]
    private string _pingTarget = "1.1.1.1";

    [ObservableProperty]
    private ConnectivityDiagnosticsResult? _pingResult;

    [ObservableProperty]
    private bool _isPingBusy;

    [ObservableProperty]
    private string _gatewayConnectivityStatus = string.Empty;

    #endregion

    #region Connections & Routing Properties

    private readonly List<ActiveConnectionInfo> _allConnections = [];
    public ObservableCollection<ActiveConnectionInfo> FilteredConnections { get; } = [];

    public ObservableCollection<ListeningPortInfo> ListeningSockets { get; } = [];
    public ObservableCollection<NetworkRouteInfo> Routes { get; } = [];
    public ObservableCollection<NeighborCacheInfo> Neighbors { get; } = [];

    [ObservableProperty]
    private string _connectionSearchFilter = string.Empty;

    [ObservableProperty]
    private bool _isConnectionsBusy;

    #endregion

    #region TLS Properties

    [ObservableProperty]
    private string _tlsHost = "github.com";

    [ObservableProperty]
    private int _tlsPort = 443;

    [ObservableProperty]
    private TlsInspectionResult? _tlsResult;

    [ObservableProperty]
    private bool _isTlsBusy;

    #endregion

    #region Headers Properties

    [ObservableProperty]
    private string _httpInspectUrl = "https://github.com";

    [ObservableProperty]
    private HttpSecurityHeadersResult? _headersResult;

    [ObservableProperty]
    private bool _isHeadersBusy;

    #endregion

    #region Firewall & Security Properties

    [ObservableProperty]
    private FirewallStatusInfo? _firewallStatus;

    [ObservableProperty]
    private WindowsSecurityStatusInfo? _windowsSecurity;

    [ObservableProperty]
    private string _publicIpText = "Click to inspect public IP";

    [ObservableProperty]
    private bool _isFirewallBusy;

    #endregion

    #region Security Checks Properties

    [ObservableProperty]
    private LocalSecurityCheckResult? _securityChecksResult;

    [ObservableProperty]
    private bool _isSecurityChecksBusy;

    #endregion

    #region File Integrity Properties

    [ObservableProperty]
    private string _integrityFilePath = string.Empty;

    [ObservableProperty]
    private string _integrityExpectedHash = string.Empty;

    [ObservableProperty]
    private DeveloperHashAlgorithm _selectedIntegrityAlgorithm = DeveloperHashAlgorithm.SHA256;

    [ObservableProperty]
    private FileIntegrityComparisonResult? _integrityResult;

    [ObservableProperty]
    private bool _isIntegrityBusy;

    [ObservableProperty]
    private double _integrityProgress;

    #endregion

    #region Reports Properties

    [ObservableProperty]
    private SecurityReportFormat _reportFormat = SecurityReportFormat.Text;

    [ObservableProperty]
    private string _reportContent = string.Empty;

    [ObservableProperty]
    private bool _isReportBusy;

    [ObservableProperty]
    private string _reportStatusMessage = string.Empty;

    #endregion

    public NetworkSecurityViewModel(
        INetworkOverviewService networkService,
        IDnsDiagnosticService dnsService,
        IConnectivityService connectivityService,
        INetworkConnectionService connectionService,
        IRoutingNeighborService routingService,
        ITlsInspectionService tlsService,
        IHttpSecurityService httpSecurityService,
        IIpDomainService ipDomainService,
        IFirewallStatusService firewallService,
        IWindowsSecurityService windowsSecurityService,
        ILocalSecurityCheckService securityCheckService,
        IFileIntegrityService fileIntegrityService,
        ISecurityReportService reportService,
        ILogger<NetworkSecurityViewModel> logger)
    {
        _networkService = networkService;
        _dnsService = dnsService;
        _connectivityService = connectivityService;
        _connectionService = connectionService;
        _routingService = routingService;
        _tlsService = tlsService;
        _httpSecurityService = httpSecurityService;
        _ipDomainService = ipDomainService;
        _firewallService = firewallService;
        _windowsSecurityService = windowsSecurityService;
        _securityCheckService = securityCheckService;
        _fileIntegrityService = fileIntegrityService;
        _reportService = reportService;
        _logger = logger;
    }

    public async Task InitializeSectionAsync(string section)
    {
        SelectedSection = section;

        switch (section)
        {
            case "Overview":
                await RefreshOverviewAsync();
                break;
            case "Adapters":
                await RefreshAdaptersAsync();
                break;
            case "DnsPing":
                if (DnsResult == null) await ExecuteDnsQueryAsync();
                if (PingResult == null) await ExecutePingAsync();
                break;
            case "Connections":
                await RefreshConnectionsAsync();
                break;
            case "Tls":
                if (TlsResult == null) await InspectTlsAsync();
                break;
            case "Headers":
                if (HeadersResult == null) await InspectHeadersAsync();
                break;
            case "Firewall":
                await RefreshFirewallAsync();
                break;
            case "Posture":
            case "SecurityChecks":
                await RunSecurityChecksAsync();
                break;
            case "Integrity":
                break;
            case "Report":
                if (Overview == null) await RefreshOverviewAsync();
                if (SecurityChecksResult == null) await RunSecurityChecksAsync();
                await GenerateReportAsync();
                break;
        }
    }

    #region Overview Commands

    [RelayCommand]
    public async Task RefreshOverviewAsync()
    {
        IsOverviewBusy = true;
        try
        {
            Overview = await _networkService.GetNetworkOverviewAsync();
            if (Adapters.Count == 0)
            {
                await RefreshAdaptersAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh network overview.");
        }
        finally
        {
            IsOverviewBusy = false;
        }
    }

    #endregion

    #region Adapters Commands

    [RelayCommand]
    public async Task RefreshAdaptersAsync()
    {
        IsAdaptersBusy = true;
        try
        {
            var list = await _networkService.GetAdaptersAsync();
            Adapters.Clear();
            foreach (var a in list) Adapters.Add(a);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh network adapters.");
        }
        finally
        {
            IsAdaptersBusy = false;
        }
    }

    #endregion

    #region DNS Commands

    [RelayCommand]
    public async Task ExecuteDnsQueryAsync()
    {
        if (string.IsNullOrWhiteSpace(DnsDomain)) return;
        IsDnsBusy = true;
        try
        {
            DnsResult = await _dnsService.QueryDnsAsync(DnsDomain, SelectedDnsRecordType);
        }
        finally
        {
            IsDnsBusy = false;
        }
    }

    #endregion

    #region Connectivity Commands

    [RelayCommand]
    public async Task ExecutePingAsync()
    {
        if (string.IsNullOrWhiteSpace(PingTarget)) return;

        IsPingBusy = true;
        _pingCts?.Dispose();
        _pingCts = new CancellationTokenSource();

        try
        {
            PingResult = await _connectivityService.PingAsync(PingTarget, count: 4, timeoutMs: 2000, cancellationToken: _pingCts.Token);
        }
        finally
        {
            IsPingBusy = false;
        }
    }

    [RelayCommand]
    public void CancelPing()
    {
        _pingCts?.Cancel();
    }

    [RelayCommand]
    public async Task TestGatewayAsync()
    {
        GatewayConnectivityStatus = "Testing gateway connectivity...";
        bool ok = await _connectivityService.TestGatewayConnectivityAsync();
        GatewayConnectivityStatus = ok ? "Default gateway is responsive." : "Default gateway did not respond.";
    }

    #endregion

    #region Connections & Routing Commands

    [RelayCommand]
    public async Task RefreshConnectionsAsync()
    {
        IsConnectionsBusy = true;
        try
        {
            var conns = await _connectionService.GetActiveConnectionsAsync();
            _allConnections.Clear();
            _allConnections.AddRange(conns);
            ApplyConnectionFilter();

            var sockets = await _connectionService.GetListeningSocketsAsync();
            ListeningSockets.Clear();
            foreach (var s in sockets) ListeningSockets.Add(s);

            var routes = await _routingService.GetRoutingTableAsync();
            Routes.Clear();
            foreach (var r in routes) Routes.Add(r);

            var neighbors = await _routingService.GetNeighborCacheAsync();
            Neighbors.Clear();
            foreach (var n in neighbors) Neighbors.Add(n);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh network connections.");
        }
        finally
        {
            IsConnectionsBusy = false;
        }
    }

    partial void OnConnectionSearchFilterChanged(string value)
    {
        ApplyConnectionFilter();
    }

    private void ApplyConnectionFilter()
    {
        FilteredConnections.Clear();
        string q = (ConnectionSearchFilter ?? string.Empty).Trim().ToLowerInvariant();
        foreach (var c in _allConnections)
        {
            if (string.IsNullOrEmpty(q) ||
                c.LocalEndpoint.Contains(q) ||
                c.RemoteEndpoint.Contains(q) ||
                c.ProcessName.ToLowerInvariant().Contains(q) ||
                c.State.ToLowerInvariant().Contains(q))
            {
                FilteredConnections.Add(c);
            }
        }
    }

    #endregion

    #region TLS Commands

    [RelayCommand]
    public async Task InspectTlsAsync()
    {
        if (string.IsNullOrWhiteSpace(TlsHost)) return;

        IsTlsBusy = true;
        try
        {
            TlsResult = await _tlsService.InspectTlsAsync(TlsHost, TlsPort);
        }
        finally
        {
            IsTlsBusy = false;
        }
    }

    #endregion

    #region Headers Commands

    [RelayCommand]
    public async Task InspectHeadersAsync()
    {
        if (string.IsNullOrWhiteSpace(HttpInspectUrl)) return;

        IsHeadersBusy = true;
        try
        {
            HeadersResult = await _httpSecurityService.InspectHeadersAsync(HttpInspectUrl);
        }
        finally
        {
            IsHeadersBusy = false;
        }
    }

    #endregion

    #region Firewall & Security Commands

    [RelayCommand]
    public async Task RefreshFirewallAsync()
    {
        IsFirewallBusy = true;
        try
        {
            FirewallStatus = await _firewallService.GetFirewallStatusAsync();
            WindowsSecurity = await _windowsSecurityService.GetWindowsSecurityStatusAsync();
        }
        finally
        {
            IsFirewallBusy = false;
        }
    }

    [RelayCommand]
    public async Task LookupPublicIpAsync()
    {
        PublicIpText = "Querying api.ipify.org...";
        var res = await _ipDomainService.LookupPublicIpAsync("api.ipify.org");
        PublicIpText = res.Success
            ? $"Public IP: {res.PublicIp} (via {res.Provider}, {res.ElapsedMilliseconds} ms)"
            : $"Lookup failed: {res.ErrorMessage}";
    }

    #endregion

    #region Security Checks Commands

    [RelayCommand]
    public async Task RunSecurityChecksAsync()
    {
        IsSecurityChecksBusy = true;
        try
        {
            SecurityChecksResult = await _securityCheckService.RunLocalSecurityChecksAsync();
        }
        finally
        {
            IsSecurityChecksBusy = false;
        }
    }

    #endregion

    #region File Integrity Commands

    [RelayCommand]
    public async Task VerifyIntegrityAsync()
    {
        if (string.IsNullOrWhiteSpace(IntegrityFilePath) || !File.Exists(IntegrityFilePath))
        {
            return;
        }

        IsIntegrityBusy = true;
        IntegrityProgress = 0;
        var progress = new Progress<double>(p => IntegrityProgress = Math.Round(p * 100, 1));

        try
        {
            IntegrityResult = await _fileIntegrityService.VerifyFileIntegrityAsync(
                IntegrityFilePath,
                SelectedIntegrityAlgorithm,
                IntegrityExpectedHash,
                progress);
        }
        finally
        {
            IsIntegrityBusy = false;
        }
    }

    #endregion

    #region Reports Commands

    [RelayCommand]
    public async Task GenerateReportAsync()
    {
        IsReportBusy = true;
        ReportStatusMessage = "Compiling diagnostic report...";
        try
        {
            var req = new SecurityReportRequest { Format = ReportFormat };
            var res = await _reportService.GenerateReportAsync(req);
            if (res.Success)
            {
                ReportContent = res.ReportContent;
                ReportStatusMessage = $"Report generated ({res.Format}) at {res.CreatedAt:HH:mm:ss} UTC.";
            }
            else
            {
                ReportContent = string.Empty;
                ReportStatusMessage = $"Generation failed: {res.ErrorMessage}";
            }
        }
        finally
        {
            IsReportBusy = false;
        }
    }

    [RelayCommand]
    public void CopyReport()
    {
        if (string.IsNullOrEmpty(ReportContent)) return;
        var dataPackage = new DataPackage();
        dataPackage.SetText(ReportContent);
        Clipboard.SetContent(dataPackage);
        ReportStatusMessage = "Report copied to clipboard.";
    }

    #endregion

    [RelayCommand]
    public void CopyToClipboard(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        var dataPackage = new DataPackage();
        dataPackage.SetText(text);
        Clipboard.SetContent(dataPackage);
    }
}

