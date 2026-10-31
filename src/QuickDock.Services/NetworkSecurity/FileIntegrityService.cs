using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;

namespace QuickDock.Services.NetworkSecurity;

public sealed class FileIntegrityService : IFileIntegrityService
{
    private readonly IDeveloperHashService _hashService;
    private readonly ILogger<FileIntegrityService> _logger;

    public FileIntegrityService(IDeveloperHashService hashService, ILogger<FileIntegrityService> logger)
    {
        _hashService = hashService;
        _logger = logger;
    }

    public async Task<FileIntegrityComparisonResult> VerifyFileIntegrityAsync(
        string filePath,
        DeveloperHashAlgorithm algorithm,
        string? expectedHash = null,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("File not found for integrity verification.", filePath);
        }

        var fi = new FileInfo(filePath);
        long fileSize = fi.Length;

        var hashResult = await _hashService.HashFileAsync(filePath, algorithm, progress, cancellationToken);

        string cleanExpected = (expectedHash ?? string.Empty).Trim();

        return new FileIntegrityComparisonResult
        {
            FilePath = filePath,
            FileSizeBytes = fileSize,
            Algorithm = algorithm,
            ComputedHash = hashResult.HexHash,
            ExpectedHash = cleanExpected,
            ElapsedMilliseconds = hashResult.ElapsedMilliseconds
        };
    }
}

