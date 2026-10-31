using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;

namespace QuickDock.Services.Files;

public sealed class FileHashService : IFileHashService
{
    private readonly ILogger<FileHashService> _logger;
    private const int BufferSize = 1024 * 1024; // 1 MB buffer

    public FileHashService(ILogger<FileHashService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> ComputeSha256Async(string filePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Cannot compute hash for non-existent file: {filePath}", filePath);
        }

        var fileInfo = new FileInfo(filePath);
        var totalBytes = fileInfo.Length;

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        return await ComputeSha256InternalAsync(stream, totalBytes, progress, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> ComputeSha256Async(Stream stream, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        long totalBytes = stream.CanSeek ? stream.Length : -1;
        return await ComputeSha256InternalAsync(stream, totalBytes, progress, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> ComputeSha256InternalAsync(Stream stream, long totalBytes, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        using var incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[BufferSize];
        long bytesProcessed = 0;
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
        {
            incrementalHash.AppendData(buffer, 0, bytesRead);
            bytesProcessed += bytesRead;

            if (totalBytes > 0 && progress != null)
            {
                progress.Report(Math.Clamp((double)bytesProcessed / totalBytes * 100.0, 0.0, 100.0));
            }
        }

        progress?.Report(100.0);
        byte[] hashBytes = incrementalHash.GetHashAndReset();
        return Convert.ToHexStringLower(hashBytes);
    }
}
