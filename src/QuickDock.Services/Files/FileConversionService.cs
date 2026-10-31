using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Security;
using System.Text;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;

namespace QuickDock.Services.Files;

public sealed class FileConversionService : IFileConversionService
{
    public const int MaxArchiveEntries = 10_000;
    public const long MaxDecompressedBytes = 2L * 1024 * 1024 * 1024; // 2 GB

    private readonly ILogger<FileConversionService> _logger;

    private static readonly HashSet<string> SupportedImageFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif"
    };

    public FileConversionService(ILogger<FileConversionService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool IsSupportedImageConversion(string sourceExt, string targetExt)
    {
        if (!sourceExt.StartsWith('.')) sourceExt = "." + sourceExt;
        if (!targetExt.StartsWith('.')) targetExt = "." + targetExt;

        return SupportedImageFormats.Contains(sourceExt) && SupportedImageFormats.Contains(targetExt);
    }

    public async Task<string> ConvertImageAsync(string sourcePath, string targetFormat, int quality = 90, string? outputDir = null, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException($"Source image not found: {sourcePath}", sourcePath);
        }

        var sourceExt = Path.GetExtension(sourcePath);
        var cleanTarget = targetFormat.TrimStart('.').ToLowerInvariant();
        var targetExt = "." + (cleanTarget == "jpg" ? "jpeg" : cleanTarget);

        if (!IsSupportedImageConversion(sourceExt, targetExt))
        {
            throw new NotSupportedException($"Conversion from {sourceExt} to {targetExt} is not supported.");
        }

        var dir = outputDir ?? Path.GetDirectoryName(sourcePath) ?? AppContext.BaseDirectory;
        Directory.CreateDirectory(dir);

        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var targetFileName = $"{baseName}_converted{targetExt}";
        var targetPath = Path.Combine(dir, targetFileName);

        // Ensure unique name if file exists
        int counter = 1;
        while (File.Exists(targetPath))
        {
            targetFileName = $"{baseName}_converted_{counter}{targetExt}";
            targetPath = Path.Combine(dir, targetFileName);
            counter++;
        }

        await Task.Run(() =>
        {
            using var image = Image.FromFile(sourcePath);
            ImageFormat format = cleanTarget switch
            {
                "png" => ImageFormat.Png,
                "jpeg" or "jpg" => ImageFormat.Jpeg,
                "bmp" => ImageFormat.Bmp,
                "gif" => ImageFormat.Gif,
                _ => ImageFormat.Png
            };

            if (format == ImageFormat.Jpeg)
            {
                var encoder = ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
                if (encoder != null)
                {
                    using var encoderParams = new global::System.Drawing.Imaging.EncoderParameters(1);
                    encoderParams.Param[0] = new global::System.Drawing.Imaging.EncoderParameter(global::System.Drawing.Imaging.Encoder.Quality, (long)Math.Clamp(quality, 1, 100));
                    image.Save(targetPath, encoder, encoderParams);
                    return;
                }
            }

            image.Save(targetPath, format);
        }, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Successfully converted image {Source} to {Target}", sourcePath, targetPath);
        return targetPath;
    }

    public async Task<string> CreateZipArchiveAsync(IEnumerable<string> sourcePaths, string targetZipPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var targetDir = Path.GetDirectoryName(targetZipPath);
        if (!string.IsNullOrEmpty(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        var pathsList = sourcePaths.Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
        if (pathsList.Count == 0)
        {
            throw new ArgumentException("No valid source files or directories provided for ZIP creation.");
        }

        await Task.Run(() =>
        {
            if (File.Exists(targetZipPath))
            {
                File.Delete(targetZipPath);
            }

            using var zipStream = new FileStream(targetZipPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

            int total = pathsList.Count;
            int current = 0;

            foreach (var path in pathsList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (File.Exists(path))
                {
                    var entryName = Path.GetFileName(path);
                    archive.CreateEntryFromFile(path, entryName, CompressionLevel.Optimal);
                }
                else if (Directory.Exists(path))
                {
                    var dirInfo = new DirectoryInfo(path);
                    var files = dirInfo.GetFiles("*", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var relativePath = Path.GetRelativePath(dirInfo.Parent?.FullName ?? path, file.FullName);
                        archive.CreateEntryFromFile(file.FullName, relativePath, CompressionLevel.Optimal);
                    }
                }

                current++;
                progress?.Report((double)current / total * 100.0);
            }
        }, cancellationToken).ConfigureAwait(false);

        return targetZipPath;
    }

    public async Task<int> ExtractZipArchiveAsync(string zipFilePath, string destinationDirectory, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(zipFilePath))
        {
            throw new FileNotFoundException($"ZIP archive not found: {zipFilePath}", zipFilePath);
        }

        var destFull = Path.GetFullPath(destinationDirectory);
        if (!destFull.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
        {
            destFull += Path.DirectorySeparatorChar;
        }

        Directory.CreateDirectory(destFull);

        return await Task.Run(() =>
        {
            using var archive = ZipFile.OpenRead(zipFilePath);
            int extracted = 0;
            int total = archive.Entries.Count;

            if (total > MaxArchiveEntries)
            {
                _logger.LogWarning("ZIP bomb protection triggered: entry count {Total} exceeds maximum {Max}", total, MaxArchiveEntries);
                throw new SecurityException($"ZIP archive contains {total} entries, exceeding the maximum safe limit of {MaxArchiveEntries}.");
            }

            long totalDecompressedBytes = 0;

            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Path traversal protection (Zip Slip)
                var destinationPath = Path.GetFullPath(Path.Combine(destFull, entry.FullName));
                if (!destinationPath.StartsWith(destFull, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Zip Slip attempt detected! Entry: {EntryName}", entry.FullName);
                    throw new SecurityException($"Malicious ZIP entry detected: '{entry.FullName}' traverses outside destination directory.");
                }

                totalDecompressedBytes += entry.Length;
                if (totalDecompressedBytes > MaxDecompressedBytes)
                {
                    _logger.LogWarning("ZIP bomb protection triggered: decompressed size {Bytes} exceeds maximum {Max}", totalDecompressedBytes, MaxDecompressedBytes);
                    throw new SecurityException($"ZIP archive decompressed size exceeds maximum safe threshold of {MaxDecompressedBytes / (1024 * 1024 * 1024)} GB.");
                }

                // If it's a directory entry
                if (string.IsNullOrEmpty(entry.Name) && (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\')))
                {
                    Directory.CreateDirectory(destinationPath);
                    continue;
                }

                var entryDir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(entryDir))
                {
                    Directory.CreateDirectory(entryDir);
                }

                entry.ExtractToFile(destinationPath, overwrite: true);
                extracted++;

                if (total > 0)
                {
                    progress?.Report((double)extracted / total * 100.0);
                }
            }

            return extracted;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> ConvertTextEncodingAsync(string sourcePath, Encoding targetEncoding, string? outputDir = null, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException($"Source text file not found: {sourcePath}", sourcePath);
        }

        var dir = outputDir ?? Path.GetDirectoryName(sourcePath) ?? AppContext.BaseDirectory;
        Directory.CreateDirectory(dir);

        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var ext = Path.GetExtension(sourcePath);
        var targetPath = Path.Combine(dir, $"{baseName}_{targetEncoding.WebName}{ext}");

        int counter = 1;
        while (File.Exists(targetPath))
        {
            targetPath = Path.Combine(dir, $"{baseName}_{targetEncoding.WebName}_{counter}{ext}");
            counter++;
        }

        using var reader = new StreamReader(sourcePath, detectEncodingFromByteOrderMarks: true);
        using var writer = new StreamWriter(targetPath, false, targetEncoding);

        char[] buffer = new char[8192];
        int charsRead;
        while ((charsRead = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
        {
            await writer.WriteAsync(buffer.AsMemory(0, charsRead), cancellationToken).ConfigureAwait(false);
        }

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        return targetPath;
    }
}
