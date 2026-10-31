using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;
using QuickDock.Infrastructure.Storage;

namespace QuickDock.Services.QuickDrop;

public sealed class QuickDropHttpServer : IDisposable
{
    private readonly ILogger<QuickDropHttpServer> _logger;
    private readonly IFileTransferManager _transferManager;
    private readonly INotificationService _notificationService;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;

    public QuickDropSession? CurrentSession { get; private set; }
    public bool IsRunning => _listener != null && CurrentSession != null && CurrentSession.IsActive;

    public event EventHandler<TransferItem>? FileReceived;

    public QuickDropHttpServer(
        IFileTransferManager transferManager,
        INotificationService notificationService,
        ILogger<QuickDropHttpServer> logger)
    {
        _transferManager = transferManager ?? throw new ArgumentNullException(nameof(transferManager));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Start(QuickDropSession session)
    {
        if (IsRunning)
        {
            _logger.LogWarning("QuickDrop server is already running. Stopping previous instance.");
            Stop();
        }

        CurrentSession = session ?? throw new ArgumentNullException(nameof(session));
        _cts = new CancellationTokenSource();

        var ip = IPAddress.Parse(session.LocalAddress);
        _listener = new TcpListener(ip, session.Port);
        _listener.Start();
        if (session.Port == 0 && _listener.LocalEndpoint is IPEndPoint ep)
        {
            session.Port = ep.Port;
        }
        session.IsActive = true;

        _listenerTask = Task.Run(() => AcceptConnectionsAsync(_listener, _cts.Token));
        _logger.LogInformation("QuickDrop HTTP Server started on http://{Address}:{Port}", session.LocalAddress, session.Port);
    }

    public void Stop()
    {
        try
        {
            _cts?.Cancel();
            _listener?.Stop();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Exception while stopping QuickDrop listener");
        }
        finally
        {
            _listener = null;
            if (CurrentSession != null)
            {
                CurrentSession.IsActive = false;
            }
            _logger.LogInformation("QuickDrop HTTP Server stopped.");
        }
    }

    private async Task AcceptConnectionsAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested) break;
                _logger.LogDebug(ex, "QuickDrop connection accept error");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            try
            {
                using var stream = client.GetStream();

                // Read header bytes without consuming any body bytes into a StreamReader buffer
                var headerBytes = new List<byte>(512);
                byte[] b = new byte[1];
                while (headerBytes.Count < 32768 && await stream.ReadAsync(b.AsMemory(0, 1), cancellationToken).ConfigureAwait(false) > 0)
                {
                    headerBytes.Add(b[0]);
                    int len = headerBytes.Count;
                    if (len >= 4 &&
                        headerBytes[len - 4] == (byte)'\r' && headerBytes[len - 3] == (byte)'\n' &&
                        headerBytes[len - 2] == (byte)'\r' && headerBytes[len - 1] == (byte)'\n')
                    {
                        break;
                    }
                }

                if (headerBytes.Count == 0) return;

                var headerText = Encoding.UTF8.GetString(headerBytes.ToArray());
                var lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);
                if (lines.Length == 0 || string.IsNullOrWhiteSpace(lines[0])) return;

                var requestLine = lines[0];
                var parts = requestLine.Split(' ');
                if (parts.Length < 2) return;

                var method = parts[0].ToUpperInvariant();
                var rawUrl = parts[1];

                // Read headers
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var colonIdx = line.IndexOf(':');
                    if (colonIdx > 0)
                    {
                        var key = line[..colonIdx].Trim();
                        var val = line[(colonIdx + 1)..].Trim();
                        headers[key] = val;
                    }
                }

                // Parse path and query parameters
                var uri = new Uri($"http://localhost{rawUrl}");
                var queryParams = ParseQuery(uri.Query);

                var token = queryParams.GetValueOrDefault("token");

                // Validate session and token
                if (CurrentSession == null || !CurrentSession.IsActive ||
                    DateTimeOffset.UtcNow > CurrentSession.ExpiresAt ||
                    !string.Equals(token, CurrentSession.Token, StringComparison.Ordinal))
                {
                    await SendResponseAsync(stream, 403, "text/plain", "Forbidden: Invalid or expired pairing token.").ConfigureAwait(false);
                    return;
                }

                var path = uri.AbsolutePath.TrimEnd('/');

                if (method == "GET" && (path == "" || path == "/index.html"))
                {
                    var html = GenerateMobileWebUi(CurrentSession);
                    await SendResponseAsync(stream, 200, "text/html; charset=utf-8", html).ConfigureAwait(false);
                }
                else if (method == "GET" && path == "/api/info")
                {
                    var remaining = Math.Max(0, (int)(CurrentSession.ExpiresAt - DateTimeOffset.UtcNow).TotalSeconds);
                    var files = CurrentSession.SharedFiles.Values.Select(f => new
                    {
                        f.FileId,
                        f.FileName,
                        f.Size,
                        f.FormattedSize,
                        f.Hash
                    }).ToList();

                    var infoObj = new
                    {
                        machineName = Environment.MachineName,
                        remainingSeconds = remaining,
                        sharedFiles = files
                    };

                    var json = JsonSerializer.Serialize(infoObj);
                    await SendResponseAsync(stream, 200, "application/json", json).ConfigureAwait(false);
                }
                else if (method == "GET" && path.StartsWith("/api/download/"))
                {
                    var fileId = path["/api/download/".Length..];
                    await HandleFileDownloadAsync(stream, fileId, cancellationToken).ConfigureAwait(false);
                }
                else if (method == "POST" && path == "/api/upload")
                {
                    headers.TryGetValue("Content-Length", out var lenHeader);
                    long.TryParse(lenHeader, out var contentLength);
                    var filename = queryParams.GetValueOrDefault("filename") ?? "received_file.bin";
                    await HandleFileUploadAsync(stream, filename, contentLength, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await SendResponseAsync(stream, 404, "text/plain", "Not Found").ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error handling QuickDrop client");
            }
        }
    }

    private async Task HandleFileDownloadAsync(NetworkStream networkStream, string fileId, CancellationToken cancellationToken)
    {
        if (CurrentSession == null || !CurrentSession.SharedFiles.TryGetValue(fileId, out var sharedFile))
        {
            await SendResponseAsync(networkStream, 404, "text/plain", "File not found or not shared.").ConfigureAwait(false);
            return;
        }

        if (!File.Exists(sharedFile.OriginalPath))
        {
            await SendResponseAsync(networkStream, 404, "text/plain", "Original file is missing.").ConfigureAwait(false);
            return;
        }

        var fileInfo = new FileInfo(sharedFile.OriginalPath);
        var transfer = _transferManager.RegisterTransfer(TransferDirection.Download, sharedFile.FileName, fileInfo.Length);

        try
        {
            var header = $"HTTP/1.1 200 OK\r\n" +
                         $"Content-Type: application/octet-stream\r\n" +
                         $"Content-Length: {fileInfo.Length}\r\n" +
                         $"Content-Disposition: attachment; filename=\"{sharedFile.FileName}\"\r\n" +
                         $"Connection: close\r\n\r\n";
            var headerBytes = Encoding.UTF8.GetBytes(header);
            await networkStream.WriteAsync(headerBytes, cancellationToken).ConfigureAwait(false);

            using var fileStream = new FileStream(sharedFile.OriginalPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true);
            byte[] buffer = new byte[65536];
            long transferred = 0;
            int read;
            var sw = global::System.Diagnostics.Stopwatch.StartNew();

            while ((read = await fileStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                await networkStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                transferred += read;
                var speed = sw.Elapsed.TotalSeconds > 0 ? transferred / sw.Elapsed.TotalSeconds : 0;
                _transferManager.UpdateProgress(transfer.Id, transferred, speed);
            }

            await _transferManager.CompleteTransferAsync(transfer.Id, sharedFile.Hash).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _transferManager.FailTransferAsync(transfer.Id, ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    private async Task HandleFileUploadAsync(NetworkStream networkStream, string originalName, long contentLength, CancellationToken cancellationToken)
    {
        // Sanitize file name to prevent path traversal
        var sanitizedName = Path.GetFileName(originalName);
        if (string.IsNullOrWhiteSpace(sanitizedName))
        {
            sanitizedName = $"upload_{Guid.NewGuid():N}.bin";
        }

        var targetDir = AppStoragePaths.QuickDropDirectory;
        Directory.CreateDirectory(targetDir);

        var targetPath = Path.Combine(targetDir, $"{Guid.NewGuid():N}_{sanitizedName}");
        var transfer = _transferManager.RegisterTransfer(TransferDirection.Upload, sanitizedName, contentLength);

        using var incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long transferred = 0;
        var sw = global::System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using (var fileStream = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, useAsync: true))
            {
                byte[] buffer = new byte[65536];
                int read;
                while (transferred < contentLength && (read = await networkStream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, contentLength - transferred)), cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    incrementalHash.AppendData(buffer, 0, read);
                    transferred += read;

                    var speed = sw.Elapsed.TotalSeconds > 0 ? transferred / sw.Elapsed.TotalSeconds : 0;
                    _transferManager.UpdateProgress(transfer.Id, transferred, speed);
                }
            }

            var hash = Convert.ToHexStringLower(incrementalHash.GetHashAndReset());
            await _transferManager.CompleteTransferAsync(transfer.Id, hash).ConfigureAwait(false);

            _notificationService.ShowNotification(
                "QuickDrop: File Received",
                $"Successfully received '{sanitizedName}' ({FileShelfItem.FormatBytes(transferred)}). Verified SHA-256.",
                "quickdrop.received");

            FileReceived?.Invoke(this, transfer);

            var responseJson = JsonSerializer.Serialize(new { status = "success", hash, fileName = sanitizedName });
            await SendResponseAsync(networkStream, 200, "application/json", responseJson).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _transferManager.FailTransferAsync(transfer.Id, ex.Message).ConfigureAwait(false);
            if (File.Exists(targetPath))
            {
                try { File.Delete(targetPath); } catch { }
            }
            throw;
        }
    }

    private static async Task SendResponseAsync(NetworkStream stream, int statusCode, string contentType, string content)
    {
        var contentBytes = Encoding.UTF8.GetBytes(content);
        var statusText = statusCode switch
        {
            200 => "OK",
            400 => "Bad Request",
            403 => "Forbidden",
            404 => "Not Found",
            _ => "Error"
        };

        var header = $"HTTP/1.1 {statusCode} {statusText}\r\n" +
                     $"Content-Type: {contentType}\r\n" +
                     $"Content-Length: {contentBytes.Length}\r\n" +
                     $"Access-Control-Allow-Origin: *\r\n" +
                     $"Connection: close\r\n\r\n";
        var headerBytes = Encoding.UTF8.GetBytes(header);
        await stream.WriteAsync(headerBytes).ConfigureAwait(false);
        await stream.WriteAsync(contentBytes).ConfigureAwait(false);
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(query)) return dict;

        var clean = query.TrimStart('?');
        foreach (var pair in clean.Split('&'))
        {
            var kv = pair.Split('=');
            if (kv.Length >= 1)
            {
                var key = Uri.UnescapeDataString(kv[0]);
                var val = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : string.Empty;
                dict[key] = val;
            }
        }
        return dict;
    }

    private static string GenerateMobileWebUi(QuickDropSession session)
    {
        var machine = WebUtility.HtmlEncode(Environment.MachineName);
        var token = session.Token;

        return $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1.0, user-scalable=no">
          <title>QuickDrop — {{machine}}</title>
          <style>
            :root {
              --bg: #0f1115;
              --card: #181b20;
              --border: #282c34;
              --accent: #0078d4;
              --text: #f0f2f5;
              --muted: #8b949e;
              --success: #238636;
            }
            * { box-sizing: border-box; margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; }
            body { background: var(--bg); color: var(--text); padding: 18px; max-width: 520px; margin: 0 auto; min-height: 100vh; display: flex; flex-direction: column; }
            header { display: flex; align-items: center; justify-content: space-between; padding-bottom: 16px; border-bottom: 1px solid var(--border); margin-bottom: 20px; }
            .logo { font-size: 1.25rem; font-weight: 700; color: var(--text); display: flex; align-items: center; gap: 8px; }
            .badge { background: #1c2e3d; color: #58a6ff; font-size: 0.75rem; padding: 4px 8px; border-radius: 12px; font-weight: 600; }
            .card { background: var(--card); border: 1px solid var(--border); border-radius: 12px; padding: 18px; margin-bottom: 18px; }
            h2 { font-size: 1rem; font-weight: 600; margin-bottom: 12px; display: flex; justify-content: space-between; align-items: center; }
            .drop-zone { border: 2px dashed var(--border); border-radius: 10px; padding: 24px 16px; text-align: center; cursor: pointer; transition: border-color 0.2s; }
            .drop-zone:hover { border-color: var(--accent); }
            .btn { background: var(--accent); color: white; border: none; padding: 10px 18px; border-radius: 8px; font-weight: 600; font-size: 0.9rem; cursor: pointer; display: inline-block; }
            .btn-secondary { background: #21262d; color: var(--text); border: 1px solid var(--border); text-decoration: none; padding: 6px 12px; font-size: 0.8rem; border-radius: 6px; }
            .file-list { display: flex; flex-direction: column; gap: 10px; }
            .file-item { display: flex; align-items: center; justify-content: space-between; background: #111418; padding: 12px; border-radius: 8px; border: 1px solid var(--border); }
            .file-info { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; margin-right: 12px; }
            .file-name { font-size: 0.9rem; font-weight: 500; }
            .file-meta { font-size: 0.75rem; color: var(--muted); margin-top: 2px; }
            .progress-bar-container { background: #21262d; border-radius: 6px; height: 8px; overflow: hidden; margin-top: 10px; display: none; }
            .progress-bar { background: var(--accent); height: 100%; width: 0%; transition: width 0.1s; }
            #statusText { font-size: 0.8rem; color: var(--muted); margin-top: 8px; }
            .empty-state { color: var(--muted); font-size: 0.85rem; text-align: center; padding: 16px 0; }
            footer { margin-top: auto; text-align: center; color: var(--muted); font-size: 0.75rem; padding: 16px 0; }
          </style>
        </head>
        <body>
          <header>
            <div class="logo">
              <span>QuickDrop</span>
            </div>
            <div class="badge">Connected to {{machine}}</div>
          </header>

          <div class="card">
            <h2>Send to Windows PC</h2>
            <div class="drop-zone" onclick="document.getElementById('fileInput').click()">
              <p style="margin-bottom: 12px; font-size: 0.9rem; color: var(--muted);">Tap below to select files from this device</p>
              <button class="btn" type="button">Select Files</button>
              <input type="file" id="fileInput" multiple style="display:none" onchange="uploadFiles(this.files)">
            </div>
            <div class="progress-bar-container" id="progressContainer">
              <div class="progress-bar" id="progressBar"></div>
            </div>
            <div id="statusText"></div>
          </div>

          <div class="card">
            <h2>Available on Windows PC</h2>
            <div id="sharedList" class="file-list">
              <div class="empty-state">No files shared yet by {{machine}}.</div>
            </div>
          </div>

          <footer>
            Local Wi-Fi Network &bull; End-to-End Direct &bull; No Cloud
          </footer>

          <script>
            const token = "{{token}}";
            async function refreshSharedFiles() {
              try {
                const res = await fetch('/api/info?token=' + token);
                if (!res.ok) return;
                const data = await res.json();
                const list = document.getElementById('sharedList');
                if (!data.sharedFiles || data.sharedFiles.length === 0) {
                  list.innerHTML = '<div class="empty-state">No files shared yet by {{machine}}.</div>';
                  return;
                }
                list.innerHTML = data.sharedFiles.map(f => `
                  <div class="file-item">
                    <div class="file-info">
                      <div class="file-name">${f.FileName}</div>
                      <div class="file-meta">${f.FormattedSize}</div>
                    </div>
                    <a href="/api/download/${f.FileId}?token=${token}" class="btn-secondary" download>Download</a>
                  </div>
                `).join('');
              } catch (e) {
                console.error(e);
              }
            }

            function uploadFiles(files) {
              if (!files || files.length === 0) return;
              const file = files[0];
              const container = document.getElementById('progressContainer');
              const bar = document.getElementById('progressBar');
              const status = document.getElementById('statusText');

              container.style.display = 'block';
              bar.style.width = '0%';
              status.innerText = 'Uploading ' + file.name + '...';

              const xhr = new XMLHttpRequest();
              xhr.open('POST', '/api/upload?token=' + token + '&filename=' + encodeURIComponent(file.name), true);

              xhr.upload.onprogress = function(e) {
                if (e.lengthComputable) {
                  const percent = Math.round((e.loaded / e.total) * 100);
                  bar.style.width = percent + '%';
                  status.innerText = 'Uploading: ' + percent + '% (' + Math.round(e.loaded/1024) + ' KB / ' + Math.round(e.total/1024) + ' KB)';
                }
              };

              xhr.onload = function() {
                if (xhr.status === 200) {
                  bar.style.width = '100%';
                  status.innerText = 'Upload complete! Verified SHA-256 on PC.';
                  setTimeout(() => { container.style.display = 'none'; }, 3000);
                } else {
                  status.innerText = 'Upload failed: HTTP ' + xhr.status;
                }
              };

              xhr.onerror = function() {
                status.innerText = 'Network error during upload.';
              };

              xhr.send(file);
            }

            setInterval(refreshSharedFiles, 3000);
            refreshSharedFiles();
          </script>
        </body>
        </html>
        """;
    }

    public void Dispose()
    {
        Stop();
        if (_listenerTask != null)
        {
            try
            {
                _listenerTask.Wait(TimeSpan.FromMilliseconds(500));
            }
            catch
            {
                // Task cancellation or completion
            }
            _listenerTask = null;
        }
        _cts?.Dispose();
        _cts = null;
    }
}
