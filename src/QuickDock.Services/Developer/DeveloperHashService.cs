using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;

namespace QuickDock.Services.Developer;

public sealed class DeveloperHashService : IDeveloperHashService
{
    public DeveloperHashResult HashText(string input, DeveloperHashAlgorithm algorithm)
    {
        var sw = Stopwatch.StartNew();
        byte[] bytes = Encoding.UTF8.GetBytes(input ?? string.Empty);
        using var hasher = CreateHashAlgorithm(algorithm);
        byte[] hash = hasher.ComputeHash(bytes);
        sw.Stop();

        return new DeveloperHashResult
        {
            Algorithm = algorithm,
            HexHash = Convert.ToHexStringLower(hash),
            ElapsedMilliseconds = sw.ElapsedMilliseconds,
            Source = "Text"
        };
    }

    public async Task<DeveloperHashResult> HashFileAsync(
        string filePath,
        DeveloperHashAlgorithm algorithm,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Target file not found.", filePath);
        }

        var sw = Stopwatch.StartNew();
        using var hasher = CreateIncrementalHash(algorithm);
        var fileInfo = new FileInfo(filePath);
        long totalBytes = fileInfo.Length;
        long bytesRead = 0;

        byte[] buffer = new byte[64 * 1024]; // 64 KB buffer

        await using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, buffer.Length, useAsync: true))
        {
            int read;
            while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
            {
                hasher.AppendData(buffer, 0, read);
                bytesRead += read;

                if (progress != null && totalBytes > 0)
                {
                    progress.Report((double)bytesRead / totalBytes);
                }
            }
        }

        byte[] hashBytes = hasher.GetHashAndReset();
        sw.Stop();

        return new DeveloperHashResult
        {
            Algorithm = algorithm,
            HexHash = Convert.ToHexStringLower(hashBytes),
            ElapsedMilliseconds = sw.ElapsedMilliseconds,
            Source = Path.GetFileName(filePath)
        };
    }

    private static HashAlgorithm CreateHashAlgorithm(DeveloperHashAlgorithm algorithm) => algorithm switch
    {
        DeveloperHashAlgorithm.MD5 => MD5.Create(),
        DeveloperHashAlgorithm.SHA1 => SHA1.Create(),
        DeveloperHashAlgorithm.SHA256 => SHA256.Create(),
        DeveloperHashAlgorithm.SHA384 => SHA384.Create(),
        DeveloperHashAlgorithm.SHA512 => SHA512.Create(),
        _ => SHA256.Create()
    };

    private static IncrementalHash CreateIncrementalHash(DeveloperHashAlgorithm algorithm) => algorithm switch
    {
        DeveloperHashAlgorithm.MD5 => IncrementalHash.CreateHash(HashAlgorithmName.MD5),
        DeveloperHashAlgorithm.SHA1 => IncrementalHash.CreateHash(HashAlgorithmName.SHA1),
        DeveloperHashAlgorithm.SHA256 => IncrementalHash.CreateHash(HashAlgorithmName.SHA256),
        DeveloperHashAlgorithm.SHA384 => IncrementalHash.CreateHash(HashAlgorithmName.SHA384),
        DeveloperHashAlgorithm.SHA512 => IncrementalHash.CreateHash(HashAlgorithmName.SHA512),
        _ => IncrementalHash.CreateHash(HashAlgorithmName.SHA256)
    };
}
