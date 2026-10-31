using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Modules;

namespace QuickDock.Modules.Developer;

public sealed class GitModule : ModuleBase
{
    private readonly IGitService _gitService;

    public override string Id => "quickdock.git";
    public override string Name => "Git Toolkit";
    public override string Description => "Local Git repository inspection, branches, commits, staging, diff, and push/pull.";
    public override ModuleCategory Category => ModuleCategory.Developer;

    public GitModule(IGitService gitService, ILogger<GitModule> logger)
        : base(logger)
    {
        _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
    }

    protected override Task OnInitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected override Task OnStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected override Task OnStopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class DockerModule : ModuleBase
{
    private readonly IDockerService _dockerService;

    public override string Id => "quickdock.docker";
    public override string Name => "Docker Toolkit";
    public override string Description => "Local Docker engine status, container management, logs, and image viewer.";
    public override ModuleCategory Category => ModuleCategory.Developer;

    public DockerModule(IDockerService dockerService, ILogger<DockerModule> logger)
        : base(logger)
    {
        _dockerService = dockerService ?? throw new ArgumentNullException(nameof(dockerService));
    }

    protected override Task OnInitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected override Task OnStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected override Task OnStopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class TerminalModule : ModuleBase
{
    private readonly ITerminalLauncherService _terminalService;

    public override string Id => "quickdock.terminal";
    public override string Name => "Terminal Launcher";
    public override string Description => "Native Windows Terminal, PowerShell, and Command Prompt launcher with working directory support.";
    public override ModuleCategory Category => ModuleCategory.Developer;

    public TerminalModule(ITerminalLauncherService terminalService, ILogger<TerminalModule> logger)
        : base(logger)
    {
        _terminalService = terminalService ?? throw new ArgumentNullException(nameof(terminalService));
    }

    protected override Task OnInitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected override Task OnStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected override Task OnStopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class DeveloperToolsModule : ModuleBase
{
    public override string Id => "quickdock.devtools";
    public override string Name => "Developer Utilities";
    public override string Description => "Offline utilities: JSON formatter, JWT decoder, Base64/URL encoding, Hashing, UUID, Timestamp, Regex, and QR.";
    public override ModuleCategory Category => ModuleCategory.Developer;

    public DeveloperToolsModule(ILogger<DeveloperToolsModule> logger)
        : base(logger)
    {
    }

    protected override Task OnInitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected override Task OnStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected override Task OnStopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class HttpToolkitModule : ModuleBase
{
    private readonly IHttpToolkitService _httpService;

    public override string Id => "quickdock.http";
    public override string Name => "HTTP / API Toolkit";
    public override string Description => "Lightweight REST API client with method selection, custom headers, body formatting, and timing.";
    public override ModuleCategory Category => ModuleCategory.Developer;

    public HttpToolkitModule(IHttpToolkitService httpService, ILogger<HttpToolkitModule> logger)
        : base(logger)
    {
        _httpService = httpService ?? throw new ArgumentNullException(nameof(httpService));
    }

    protected override Task OnInitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected override Task OnStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected override Task OnStopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class LocalDevModule : ModuleBase
{
    private readonly ILocalDevelopmentService _localDevService;

    public override string Id => "quickdock.localdev";
    public override string Name => "Localhost & Ports";
    public override string Description => "On-demand inspection of active listening TCP ports, owning processes, and localhost URLs.";
    public override ModuleCategory Category => ModuleCategory.Developer;

    public LocalDevModule(ILocalDevelopmentService localDevService, ILogger<LocalDevModule> logger)
        : base(logger)
    {
        _localDevService = localDevService ?? throw new ArgumentNullException(nameof(localDevService));
    }

    protected override Task OnInitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected override Task OnStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected override Task OnStopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class ProcessMonitorModule : ModuleBase
{
    private readonly IDeveloperProcessService _processService;

    public override string Id => "quickdock.processes";
    public override string Name => "Process Monitor";
    public override string Description => "Developer process inspection, memory consumption, listening port mapping, and termination.";
    public override ModuleCategory Category => ModuleCategory.Developer;

    public ProcessMonitorModule(IDeveloperProcessService processService, ILogger<ProcessMonitorModule> logger)
        : base(logger)
    {
        _processService = processService ?? throw new ArgumentNullException(nameof(processService));
    }

    protected override Task OnInitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected override Task OnStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected override Task OnStopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

