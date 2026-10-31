using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QRCoder;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;
using QuickDock.Core.Settings;

namespace QuickDock.Services.QuickDrop;

public sealed class QuickDropService : IQuickDropService, IDisposable
{
    private readonly IFileTransferManager _transferManager;
    private readonly INotificationService _notificationService;
    private readonly ISettingsManager _settingsManager;
    private readonly IFileHashService _hashService;
    private readonly ILogger<QuickDropService> _logger;
    private readonly ILogger<QuickDropHttpServer> _serverLogger;

    private QuickDropHttpServer? _server;
    private global::System.Threading.Timer? _expiryTimer;

    public QuickDropSession? CurrentSession => _server?.CurrentSession;
    public bool IsRunning => _server != null && _server.IsRunning;

    public event EventHandler<QuickDropSession?>? SessionChanged;
    public event EventHandler<QuickDropSharedFile>? FileShared;
    public event EventHandler<TransferItem>? FileReceived;

    public QuickDropService(
        IFileTransferManager transferManager,
        INotificationService notificationService,
        ISettingsManager settingsManager,
        IFileHashService hashService,
        ILogger<QuickDropService> logger,
        ILogger<QuickDropHttpServer> serverLogger)
    {
        _transferManager = transferManager ?? throw new ArgumentNullException(nameof(transferManager));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _hashService = hashService ?? throw new ArgumentNullException(nameof(hashService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serverLogger = serverLogger ?? throw new ArgumentNullException(nameof(serverLogger));
    }

    public async Task<QuickDropSession> StartSessionAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning && CurrentSession != null)
        {
            return CurrentSession;
        }

        var settings = _settingsManager.CurrentSettings;
        var localIp = GetBestLocalIpAddress();
        if (localIp == null)
        {
            throw new InvalidOperationException("No suitable local network interface found. Connect to Wi-Fi or local Ethernet.");
        }

        var port = FindAvailablePort(localIp, settings.QuickDrop.PreferredPort);
        var token = GenerateSecureToken();
        var timeoutMinutes = Math.Clamp(settings.QuickDrop.SessionTimeoutMinutes, 1, 60);

        var session = new QuickDropSession
        {
            Token = token,
            LocalAddress = localIp.ToString(),
            Port = port,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(timeoutMinutes),
            IsActive = true
        };

        _server = new QuickDropHttpServer(_transferManager, _notificationService, _serverLogger);
        _server.FileReceived += (s, e) => FileReceived?.Invoke(this, e);
        _server.Start(session);

        _expiryTimer?.Dispose();
        _expiryTimer = new global::System.Threading.Timer(_ =>
        {
            _logger.LogInformation("QuickDrop session expired.");
            _ = StopSessionAsync();
        }, null, TimeSpan.FromMinutes(timeoutMinutes), Timeout.InfiniteTimeSpan);

        SessionChanged?.Invoke(this, session);
        return session;
    }

    public Task StopSessionAsync()
    {
        _expiryTimer?.Dispose();
        _expiryTimer = null;

        if (_server != null)
        {
            _server.Stop();
            _server.Dispose();
            _server = null;
        }

        SessionChanged?.Invoke(this, null);
        return Task.CompletedTask;
    }

    public QuickDropSharedFile ShareFile(string filePath)
    {
        if (CurrentSession == null || !CurrentSession.IsActive)
        {
            throw new InvalidOperationException("QuickDrop session is not running.");
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Cannot share non-existent file: {filePath}", filePath);
        }

        var fi = new FileInfo(filePath);
        var shared = new QuickDropSharedFile
        {
            FileId = Guid.NewGuid().ToString("N"),
            OriginalPath = Path.GetFullPath(filePath),
            FileName = fi.Name,
            Size = fi.Length
        };

        CurrentSession.SharedFiles[shared.FileId] = shared;

        // Compute hash asynchronously in background
        _ = Task.Run(async () =>
        {
            try
            {
                shared.Hash = await _hashService.ComputeSha256Async(filePath).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to compute hash for shared file {Path}", filePath);
            }
        });

        FileShared?.Invoke(this, shared);
        return shared;
    }

    public bool RemoveSharedFile(string fileId)
    {
        if (CurrentSession != null && CurrentSession.SharedFiles.Remove(fileId))
        {
            return true;
        }
        return false;
    }

    public byte[] GenerateQrCodePng(string content, int pixelsPerModule = 6)
    {
        using var generator = new QRCodeGenerator();
        using var qrCodeData = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        return qrCode.GetGraphic(pixelsPerModule);
    }

    private static IPAddress? GetBestLocalIpAddress()
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                        n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .OrderByDescending(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
            .ThenByDescending(n => n.NetworkInterfaceType == NetworkInterfaceType.Ethernet);

        foreach (var nic in interfaces)
        {
            var props = nic.GetIPProperties();
            foreach (var addr in props.UnicastAddresses)
            {
                if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    var bytes = addr.Address.GetAddressBytes();
                    // Check if private IPv4 range (10.x.x.x, 172.16-31.x.x, 192.168.x.x)
                    if (bytes[0] == 10 ||
                        (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                        (bytes[0] == 192 && bytes[1] == 168))
                    {
                        return addr.Address;
                    }
                }
            }
        }

        // Fallback: any unicast IPv4
        return interfaces.SelectMany(i => i.GetIPProperties().UnicastAddresses)
            .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)?.Address;
    }

    private static int FindAvailablePort(IPAddress ip, int startPort)
    {
        int port = startPort;
        for (int i = 0; i < 50; i++)
        {
            try
            {
                var listener = new TcpListener(ip, port);
                listener.Start();
                listener.Stop();
                return port;
            }
            catch
            {
                port++;
            }
        }

        // Ephemeral port fallback
        var ephemeral = new TcpListener(ip, 0);
        ephemeral.Start();
        var selected = ((IPEndPoint)ephemeral.LocalEndpoint).Port;
        ephemeral.Stop();
        return selected;
    }

    private static string GenerateSecureToken()
    {
        byte[] tokenBytes = RandomNumberGenerator.GetBytes(12);
        return Convert.ToHexString(tokenBytes);
    }

    public void Dispose()
    {
        _ = StopSessionAsync();
    }
}
