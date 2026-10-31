using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Modules;

namespace QuickDock.Modules.Files;

public sealed class FileShelfModule : ModuleBase
{
    private readonly IFileShelfService _fileShelfService;

    public override string Id => "quickdock.fileshelf";
    public override string Name => "File Shelf";
    public override string Description => "Temporary local file staging area for opening, previewing, and converting files.";
    public override ModuleCategory Category => ModuleCategory.Utilities;

    public FileShelfModule(IFileShelfService fileShelfService, ILogger<FileShelfModule> logger)
        : base(logger)
    {
        _fileShelfService = fileShelfService ?? throw new ArgumentNullException(nameof(fileShelfService));
    }

    protected override Task OnInitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected override Task OnStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected override Task OnStopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class QuickDropModule : ModuleBase
{
    private readonly IQuickDropService _quickDropService;

    public override string Id => "quickdock.quickdrop";
    public override string Name => "QuickDrop";
    public override string Description => "Local Wi-Fi peer-to-peer file transfer between devices.";
    public override ModuleCategory Category => ModuleCategory.Utilities;

    public QuickDropModule(IQuickDropService quickDropService, ILogger<QuickDropModule> logger)
        : base(logger)
    {
        _quickDropService = quickDropService ?? throw new ArgumentNullException(nameof(quickDropService));
    }

    protected override Task OnInitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected override Task OnStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        await _quickDropService.StopSessionAsync().ConfigureAwait(false);
    }
}

public sealed class FileConverterModule : ModuleBase
{
    private readonly IFileConversionService _conversionService;

    public override string Id => "quickdock.converter";
    public override string Name => "File Converter";
    public override string Description => "Local image conversion, archive utilities, and text encoding converter.";
    public override ModuleCategory Category => ModuleCategory.Utilities;

    public FileConverterModule(IFileConversionService conversionService, ILogger<FileConverterModule> logger)
        : base(logger)
    {
        _conversionService = conversionService ?? throw new ArgumentNullException(nameof(conversionService));
    }

    protected override Task OnInitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected override Task OnStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected override Task OnStopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class TransferManagerModule : ModuleBase
{
    private readonly IFileTransferManager _transferManager;

    public override string Id => "quickdock.transfers";
    public override string Name => "Transfer Manager";
    public override string Description => "Real-time tracking and history for local and network file transfers.";
    public override ModuleCategory Category => ModuleCategory.Utilities;

    public TransferManagerModule(IFileTransferManager transferManager, ILogger<TransferManagerModule> logger)
        : base(logger)
    {
        _transferManager = transferManager ?? throw new ArgumentNullException(nameof(transferManager));
    }

    protected override Task OnInitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected override Task OnStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected override Task OnStopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

