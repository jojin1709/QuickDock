using System.Text;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;

namespace QuickDock.Services.Files;

public sealed class FilePreviewService : IFilePreviewService
{
    private readonly ILogger<FilePreviewService> _logger;
    private const int MaxTextPreviewBytes = 256 * 1024; // 256 KB max preview

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".ico"
    };

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".json", ".xml", ".csv", ".md", ".log", ".yaml", ".yml", ".ini", ".cfg", ".sql", ".cs", ".xaml"
    };

    public FilePreviewService(ILogger<FilePreviewService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<FilePreviewInfo> GeneratePreviewAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return new FilePreviewInfo
            {
                Path = filePath,
                Name = Path.GetFileName(filePath),
                Size = 0,
                Type = PreviewType.Unsupported
            };
        }

        try
        {
            var fileInfo = new FileInfo(filePath);
            var ext = fileInfo.Extension;
            var info = new FilePreviewInfo
            {
                Path = filePath,
                Name = fileInfo.Name,
                Size = fileInfo.Length
            };

            if (ImageExtensions.Contains(ext))
            {
                info.Type = PreviewType.Image;
                info.Dimensions = TryGetImageDimensions(filePath);
                return info;
            }

            if (TextExtensions.Contains(ext))
            {
                info.Type = PreviewType.Text;
                await ReadTextPreviewAsync(filePath, info, cancellationToken).ConfigureAwait(false);
                return info;
            }

            info.Type = PreviewType.Unsupported;
            return info;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate preview for {FilePath}", filePath);
            return new FilePreviewInfo
            {
                Path = filePath,
                Name = Path.GetFileName(filePath),
                Type = PreviewType.Unsupported
            };
        }
    }

    private static string? TryGetImageDimensions(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            // Quick header parsing for PNG, JPEG, BMP, GIF
            byte[] header = new byte[32];
            int read = stream.Read(header, 0, header.Length);
            if (read < 16) return null;

            // PNG signature: 89 50 4E 47 0D 0A 1A 0A
            if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
            {
                // Width at offset 16 (4 bytes big-endian), Height at offset 20 (4 bytes big-endian)
                stream.Seek(16, SeekOrigin.Begin);
                byte[] dim = new byte[8];
                stream.ReadExactly(dim, 0, 8);
                int width = (dim[0] << 24) | (dim[1] << 16) | (dim[2] << 8) | dim[3];
                int height = (dim[4] << 24) | (dim[5] << 16) | (dim[6] << 8) | dim[7];
                return $"{width} × {height}";
            }

            // BMP signature: 'B' 'M'
            if (header[0] == 0x42 && header[1] == 0x4D)
            {
                stream.Seek(18, SeekOrigin.Begin);
                byte[] dim = new byte[8];
                stream.ReadExactly(dim, 0, 8);
                int width = BitConverter.ToInt32(dim, 0);
                int height = Math.Abs(BitConverter.ToInt32(dim, 4));
                return $"{width} × {height}";
            }

            // GIF signature: 'G' 'I' 'F'
            if (header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46)
            {
                int width = header[6] | (header[7] << 8);
                int height = header[8] | (header[9] << 8);
                return $"{width} × {height}";
            }

            // Fallback for JPEG or other formats using System.Drawing
            try
            {
                using var img = global::System.Drawing.Image.FromFile(filePath);
                return $"{img.Width} × {img.Height}";
            }
            catch
            {
                return null;
            }
        }
        catch
        {
            return null;
        }
    }

    private static async Task ReadTextPreviewAsync(string filePath, FilePreviewInfo info, CancellationToken cancellationToken)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        var bytesToRead = (int)Math.Min(stream.Length, MaxTextPreviewBytes);
        byte[] buffer = new byte[bytesToRead];
        int totalRead = 0;

        while (totalRead < bytesToRead)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(totalRead, bytesToRead - totalRead), cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            totalRead += read;
        }

        info.IsTruncated = stream.Length > MaxTextPreviewBytes;

        // Detect UTF-8 vs ASCII
        var text = Encoding.UTF8.GetString(buffer, 0, totalRead);
        info.TextContent = text;
    }
}
