using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using QuickDock.Services.Files;
using Xunit;

namespace QuickDock.Core.Tests;

public class FileHashTests
{
    [Fact]
    public async Task ComputeSha256_KnownData_MatchesExpectedHash()
    {
        // Arrange
        var service = new FileHashService(NullLogger<FileHashService>.Instance);
        var inputString = "QuickDock Secure File Transfer Test 2026";
        var inputBytes = Encoding.UTF8.GetBytes(inputString);
        using var stream = new MemoryStream(inputBytes);

        // Expected hash for "QuickDock Secure File Transfer Test 2026":
        // computed via standard SHA256
        using var sha = System.Security.Cryptography.SHA256.Create();
        var expectedHash = Convert.ToHexStringLower(sha.ComputeHash(inputBytes));

        // Act
        var actualHash = await service.ComputeSha256Async(stream);

        // Assert
        Assert.Equal(expectedHash, actualHash);
    }

    [Fact]
    public async Task ComputeSha256_File_ReportsProgressAndMatches()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var data = new byte[256 * 1024]; // 256 KB
            new Random(42).NextBytes(data);
            await File.WriteAllBytesAsync(tempFile, data);

            using var sha = System.Security.Cryptography.SHA256.Create();
            var expectedHash = Convert.ToHexStringLower(sha.ComputeHash(data));

            var service = new FileHashService(NullLogger<FileHashService>.Instance);
            var progressValues = new List<double>();
            var progress = new Progress<double>(p => progressValues.Add(p));

            // Act
            var actualHash = await service.ComputeSha256Async(tempFile, progress);

            // Assert
            Assert.Equal(expectedHash, actualHash);
            Assert.Contains(progressValues, p => p >= 100.0);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ComputeSha256_NonExistentFile_ThrowsFileNotFound()
    {
        var service = new FileHashService(NullLogger<FileHashService>.Instance);
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            service.ComputeSha256Async(@"C:\NonExistent\FakeFile12345.xyz"));
    }
}
