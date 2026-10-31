using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Security;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using QuickDock.Services.Files;
using Xunit;

namespace QuickDock.Core.Tests;

public class FileConverterTests : IDisposable
{
    private readonly FileConversionService _service;
    private readonly List<string> _tempFiles = new();
    private readonly List<string> _tempDirs = new();

    public FileConverterTests()
    {
        _service = new FileConversionService(NullLogger<FileConversionService>.Instance);
    }

    private string CreateTempImage(string ext, int width = 64, int height = 64)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.{ext.TrimStart('.')}");
        using var bmp = new Bitmap(width, height);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.CornflowerBlue);
        }
        var format = ext.ToLowerInvariant() switch
        {
            "png" => ImageFormat.Png,
            "bmp" => ImageFormat.Bmp,
            "jpg" or "jpeg" => ImageFormat.Jpeg,
            _ => ImageFormat.Png
        };
        bmp.Save(path, format);
        _tempFiles.Add(path);
        return path;
    }

    [Fact]
    public async Task ConvertImage_PngToJpeg_CreatesValidFile()
    {
        // Arrange
        var source = CreateTempImage("png");

        // Act
        var target = await _service.ConvertImageAsync(source, "jpeg", quality: 85);
        _tempFiles.Add(target);

        // Assert
        Assert.True(File.Exists(target));
        Assert.EndsWith(".jpeg", target, StringComparison.OrdinalIgnoreCase);

        // Verify readable
        using var loaded = Image.FromFile(target);
        Assert.Equal(64, loaded.Width);
        Assert.Equal(64, loaded.Height);
    }

    [Fact]
    public async Task ConvertImage_UnsupportedFormat_ThrowsNotSupportedException()
    {
        var source = CreateTempImage("png");
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _service.ConvertImageAsync(source, "xyz123"));
    }

    [Fact]
    public async Task CreateAndExtractZip_SafeFiles_ExtractsSuccessfully()
    {
        // Arrange
        var tempSourceFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}_file.txt");
        File.WriteAllText(tempSourceFile, "Hello QuickDock Archive");
        _tempFiles.Add(tempSourceFile);

        var targetZip = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}_archive.zip");
        _tempFiles.Add(targetZip);

        var extractDir = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}_extracted");
        _tempDirs.Add(extractDir);

        // Act 1: Create ZIP
        await _service.CreateZipArchiveAsync(new[] { tempSourceFile }, targetZip);
        Assert.True(File.Exists(targetZip));

        // Act 2: Extract ZIP
        int extractedCount = await _service.ExtractZipArchiveAsync(targetZip, extractDir);

        // Assert
        Assert.Equal(1, extractedCount);
        var extractedFilePath = Path.Combine(extractDir, Path.GetFileName(tempSourceFile));
        Assert.True(File.Exists(extractedFilePath));
        Assert.Equal("Hello QuickDock Archive", File.ReadAllText(extractedFilePath));
    }

    [Fact]
    public async Task ExtractZip_MaliciousZipSlip_ThrowsSecurityException()
    {
        // Arrange: Craft a ZIP archive with a path traversal entry
        var maliciousZip = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}_slip.zip");
        _tempFiles.Add(maliciousZip);

        using (var zipStream = new FileStream(maliciousZip, FileMode.Create))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
        {
            var evilEntry = archive.CreateEntry("../../evil_payload.bat");
            using var writer = new StreamWriter(evilEntry.Open());
            writer.Write("echo Hacked");
        }

        var extractDir = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}_dest");
        _tempDirs.Add(extractDir);

        // Act & Assert: Must detect traversal and abort
        await Assert.ThrowsAsync<SecurityException>(() =>
            _service.ExtractZipArchiveAsync(maliciousZip, extractDir));
    }

    [Fact]
    public async Task ConvertTextEncoding_Utf8ToAscii_CreatesNewFile()
    {
        // Arrange
        var source = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        File.WriteAllText(source, "Standard ASCII Text", Encoding.UTF8);
        _tempFiles.Add(source);

        // Act
        var target = await _service.ConvertTextEncodingAsync(source, Encoding.ASCII);
        _tempFiles.Add(target);

        // Assert
        Assert.True(File.Exists(target));
        Assert.NotEqual(source, target);
        var content = File.ReadAllText(target, Encoding.ASCII);
        Assert.Equal("Standard ASCII Text", content);
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            if (File.Exists(file))
            {
                try { File.Delete(file); } catch { }
            }
        }
        foreach (var dir in _tempDirs)
        {
            if (Directory.Exists(dir))
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }
    }
}
