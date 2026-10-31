using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Modules;

namespace QuickDock.Modules.NetworkSecurity;

public sealed class NetworkDiagnosticsModule : ModuleBase
{
    private readonly INetworkOverviewService _networkService;

    public override string Id => "quickdock.networkdiag";
    public override string Name => "Network Diagnostics";
    public override string Description => "Local network adapters, IPv4/IPv6 address categorization, default gateway, and ping diagnostics.";
    public override ModuleCategory Category => ModuleCategory.Utilities;

    public NetworkDiagnosticsModule(INetworkOverviewService networkService, ILogger<NetworkDiagnosticsModule> logger)
        : base(logger)
    {
        _networkService = networkService ?? throw new ArgumentNullException(nameof(networkService));
    }

    protected override Task OnInitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected override Task OnStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected override Task OnStopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class SecurityDiagnosticsModule : ModuleBase
{
    private readonly ILocalSecurityCheckService _securityService;

    public override string Id => "quickdock.securitydiag";
    public override string Name => "Security Diagnostics";
    public override string Description => "Local defensive security checks, UAC, Secure Boot, elevation state, and diagnostic report generator.";
    public override ModuleCategory Category => ModuleCategory.Utilities;

    public SecurityDiagnosticsModule(ILocalSecurityCheckService securityService, ILogger<SecurityDiagnosticsModule> logger)
        : base(logger)
    {
        _securityService = securityService ?? throw new ArgumentNullException(nameof(securityService));
    }

    protected override Task OnInitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected override Task OnStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected override Task OnStopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class TlsInspectorModule : ModuleBase
{
    private readonly ITlsInspectionService _tlsService;

    public override string Id => "quickdock.tls";
    public override string Name => "TLS & Certificate Inspector";
    public override string Description => "On-demand TLS handshake inspection, certificate chain analysis, and expiration tracking.";
    public override ModuleCategory Category => ModuleCategory.Utilities;

    public TlsInspectorModule(ITlsInspectionService tlsService, ILogger<TlsInspectorModule> logger)
        : base(logger)
    {
        _tlsService = tlsService ?? throw new ArgumentNullException(nameof(tlsService));
    }

    protected override Task OnInitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected override Task OnStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected override Task OnStopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class HttpSecurityModule : ModuleBase
{
    private readonly IHttpSecurityService _httpSecurityService;

    public override string Id => "quickdock.httpsecurity";
    public override string Name => "HTTP Security Headers";
    public override string Description => "On-demand inspection of web application defensive headers (CSP, HSTS, XFO, nosniff, etc.).";
    public override ModuleCategory Category => ModuleCategory.Utilities;

    public HttpSecurityModule(IHttpSecurityService httpSecurityService, ILogger<HttpSecurityModule> logger)
        : base(logger)
    {
        _httpSecurityService = httpSecurityService ?? throw new ArgumentNullException(nameof(httpSecurityService));
    }

    protected override Task OnInitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected override Task OnStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected override Task OnStopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class FirewallStatusModule : ModuleBase
{
    private readonly IFirewallStatusService _firewallService;

    public override string Id => "quickdock.firewall";
    public override string Name => "Firewall & Security Status";
    public override string Description => "Read-only inspection of Windows Defender Firewall profiles and Microsoft Defender protection state.";
    public override ModuleCategory Category => ModuleCategory.Utilities;

    public FirewallStatusModule(IFirewallStatusService firewallService, ILogger<FirewallStatusModule> logger)
        : base(logger)
    {
        _firewallService = firewallService ?? throw new ArgumentNullException(nameof(firewallService));
    }

    protected override Task OnInitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected override Task OnStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected override Task OnStopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
