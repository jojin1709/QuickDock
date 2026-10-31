using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;
using QuickDock.Infrastructure.Storage;
using QuickDock.Services.Files;
using QuickDock.Services.QuickDrop;
using Xunit;

namespace QuickDock.Core.Tests;

public class QuickDropSecurityTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteDatabaseService _db;
    private readonly FileTransferManager _transferManager;
    private readonly QuickDropHttpServer _server;
    private readonly MockNotificationService _notif;
    private int _port;

    public QuickDropSecurityTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"quickdock_drop_sec_{Guid.NewGuid():N}.db");
        _db = new SqliteDatabaseService(NullLogger<SqliteDatabaseService>.Instance, _testDbPath);
        _transferManager = new FileTransferManager(_db, NullLogger<FileTransferManager>.Instance);
        _notif = new MockNotificationService();
        _server = new QuickDropHttpServer(_transferManager, _notif, NullLogger<QuickDropHttpServer>.Instance);
    }

    private QuickDropSession StartTestServer(int timeoutSeconds = 600)
    {
        _port = new Random().Next(49000, 55000);
        var session = new QuickDropSession
        {
            Token = Guid.NewGuid().ToString("N").ToUpperInvariant(),
            LocalAddress = "127.0.0.1",
            Port = _port,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(timeoutSeconds),
            IsActive = true
        };

        _server.Start(session);
        return session;
    }

    [Fact]
    public async Task Request_WithoutToken_Returns403Forbidden()
    {
        // Arrange
        StartTestServer();
        using var client = new HttpClient();

        // Act
        var response = await client.GetAsync($"http://127.0.0.1:{_port}/api/info");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Request_WithInvalidToken_Returns403Forbidden()
    {
        // Arrange
        StartTestServer();
        using var client = new HttpClient();

        // Act
        var response = await client.GetAsync($"http://127.0.0.1:{_port}/api/info?token=INVALID_TOKEN_123");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Request_WithExpiredToken_Returns403Forbidden()
    {
        // Arrange: Session expired 1 second ago
        var session = StartTestServer(timeoutSeconds: -1);
        using var client = new HttpClient();

        // Act
        var response = await client.GetAsync($"http://127.0.0.1:{_port}/api/info?token={session.Token}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Request_WithValidToken_Returns200OKAndCorrectMachine()
    {
        // Arrange
        var session = StartTestServer();
        using var client = new HttpClient();

        // Act
        var response = await client.GetAsync($"http://127.0.0.1:{_port}/api/info?token={session.Token}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(Environment.MachineName, body);
    }

    [Fact]
    public async Task Download_InvalidFileId_Returns404NotFound()
    {
        // Arrange
        var session = StartTestServer();
        using var client = new HttpClient();

        // Act: attempt to request an arbitrary file or unshared ID
        var response = await client.GetAsync($"http://127.0.0.1:{_port}/api/download/nonexistent_id?token={session.Token}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Upload_PathTraversalFilename_IsSanitizedAndSavedSafely()
    {
        // Arrange
        var session = StartTestServer();
        using var client = new HttpClient();

        // Attempt path traversal filename: "../../evil.exe"
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes("Safe payload test"));
        var maliciousFilename = Uri.EscapeDataString(@"../../evil.exe");

        // Act
        var response = await client.PostAsync($"http://127.0.0.1:{_port}/api/upload?token={session.Token}&filename={maliciousFilename}", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var respBody = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(respBody);
        var savedName = doc.RootElement.GetProperty("fileName").GetString();

        // Verified: The traversal components are stripped, keeping only base filename "evil.exe"
        Assert.Equal("evil.exe", savedName);
    }

    public void Dispose()
    {
        _server.Dispose();
        _db.Dispose();
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }
}
