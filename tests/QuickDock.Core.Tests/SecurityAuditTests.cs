using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;
using QuickDock.Infrastructure.Storage;
using QuickDock.Services.Developer;
using QuickDock.Services.Files;
using QuickDock.Services.QuickDrop;
using Xunit;

namespace QuickDock.Core.Tests;

public class SecurityAuditTests : IDisposable
{
    private readonly string _tempDir;

    public SecurityAuditTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"quickdock_sec_audit_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public void TimingSafeTokenEquals_MatchesIdenticalTokens()
    {
        string tokenA = "A1B2C3D4E5F6789012345678";
        string tokenB = "A1B2C3D4E5F6789012345678";

        Assert.True(QuickDropHttpServer.TimingSafeTokenEquals(tokenA, tokenB));
    }

    [Theory]
    [InlineData("A1B2C3D4E5F6789012345678", "A1B2C3D4E5F6789012345679")]
    [InlineData("A1B2C3D4E5F6789012345678", "short")]
    [InlineData("A1B2C3D4E5F6789012345678", "")]
    [InlineData(null, "token")]
    [InlineData("token", null)]
    public void TimingSafeTokenEquals_RejectsMismatchedOrNullTokens(string? a, string? b)
    {
        Assert.False(QuickDropHttpServer.TimingSafeTokenEquals(a, b));
    }

    [Theory]
    [InlineData("CON.txt")]
    [InlineData("PRN")]
    [InlineData("AUX.dat")]
    [InlineData("NUL")]
    [InlineData("com1.bin")]
    [InlineData("lpt9.log")]
    public void SanitizeUploadedFileName_ReplacesDosDeviceNames(string reservedName)
    {
        string sanitized = QuickDropHttpServer.SanitizeUploadedFileName(reservedName);

        Assert.StartsWith("upload_", sanitized);
        Assert.DoesNotContain(reservedName, sanitized, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(@"..\..\..\Windows\System32\cmd.exe", "cmd.exe")]
    [InlineData(@"/var/log/../../evil.sh", "evil.sh")]
    [InlineData(@"sub\folder\file.png", "file.png")]
    [InlineData("safe_document.pdf", "safe_document.pdf")]
    public void SanitizeUploadedFileName_StripsDirectoryTraversalPaths(string inputPath, string expectedEnding)
    {
        string sanitized = QuickDropHttpServer.SanitizeUploadedFileName(inputPath);

        Assert.EndsWith(expectedEnding, sanitized);
        Assert.DoesNotContain("..", sanitized);
        Assert.DoesNotContain("/", sanitized);
        Assert.DoesNotContain("\\", sanitized);
    }

    [Theory]
    [InlineData("test\"file.exe")]
    [InlineData("test;rm -rf /")]
    [InlineData("test|calc.exe")]
    [InlineData("test&notepad.exe")]
    [InlineData("test\nnewline.txt")]
    public void TerminalLauncher_SanitizeWorkingDirectory_RejectsShellMetacharacters(string dangerousPath)
    {
        string sanitized = TerminalLauncherService.SanitizeWorkingDirectory(dangerousPath);
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        Assert.Equal(userProfile, sanitized);
    }

    [Fact]
    public void TerminalLauncher_SanitizeWorkingDirectory_AcceptsValidExistingDirectory()
    {
        string sanitized = TerminalLauncherService.SanitizeWorkingDirectory(_tempDir);

        Assert.Equal(Path.GetFullPath(_tempDir), Path.GetFullPath(sanitized));
    }

    [Fact]
    public void DeveloperProcessService_TerminateProcess_RefusesToTerminateProtectedProcesses()
    {
        var localDev = new LocalDevelopmentService(NullLogger<LocalDevelopmentService>.Instance);
        var svc = new DeveloperProcessService(localDev, NullLogger<DeveloperProcessService>.Instance);

        // System Idle (0)
        Assert.False(svc.TerminateProcess(0));
        // System (4)
        Assert.False(svc.TerminateProcess(4));
        // Current QuickDock Host PID
        Assert.False(svc.TerminateProcess(Environment.ProcessId));
    }

    [Fact]
    public async Task FileConversion_ZipSlip_ThrowsSecurityException()
    {
        string zipPath = Path.Combine(_tempDir, "zipslip.zip");
        string extractDest = Path.Combine(_tempDir, "extracted");
        Directory.CreateDirectory(extractDest);

        using (var zipStream = new FileStream(zipPath, FileMode.Create))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("../../../evil_outside.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.WriteLine("malicious payload");
        }

        var svc = new FileConversionService(NullLogger<FileConversionService>.Instance);

        await Assert.ThrowsAsync<SecurityException>(() => svc.ExtractZipArchiveAsync(zipPath, extractDest));
    }

    [Fact]
    public async Task FileConversion_ZipBombEntryCount_ThrowsSecurityException()
    {
        string zipPath = Path.Combine(_tempDir, "zipbomb_entries.zip");
        string extractDest = Path.Combine(_tempDir, "extracted_bomb");
        Directory.CreateDirectory(extractDest);

        // Create an archive with entries exceeding MaxArchiveEntries (10,000)
        using (var zipStream = new FileStream(zipPath, FileMode.Create))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
        {
            for (int i = 0; i <= FileConversionService.MaxArchiveEntries; i++)
            {
                var entry = archive.CreateEntry($"entry_{i}.txt");
                using var stream = entry.Open();
                stream.WriteByte(65); // 1 byte
            }
        }

        var svc = new FileConversionService(NullLogger<FileConversionService>.Instance);

        var ex = await Assert.ThrowsAsync<SecurityException>(() => svc.ExtractZipArchiveAsync(zipPath, extractDest));
        Assert.Contains("maximum safe limit", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("file:///C:/Windows/System32/cmd.exe")]
    [InlineData("ftp://ftp.example.com/file.txt")]
    [InlineData("gopher://gopher.floodgap.com")]
    [InlineData("javascript:alert(1)")]
    public async Task HttpToolkit_SendRequest_RejectsDisallowedProtocols(string forbiddenUrl)
    {
        using var httpClient = new HttpClient();
        var svc = new HttpToolkitService(httpClient, NullLogger<HttpToolkitService>.Instance);

        var request = new HttpRequestModel
        {
            Method = "GET",
            Url = forbiddenUrl
        };

        var response = await svc.SendRequestAsync(request);

        Assert.Equal(0, response.StatusCode);
        Assert.Equal("Invalid Scheme", response.StatusText);
        Assert.Contains("Only http and https", response.ErrorMessage);
    }

    [Fact]
    public async Task SqliteDatabaseService_ParameterizedQueries_PreventsSqlInjection()
    {
        string dbPath = Path.Combine(_tempDir, "sqli_test.db");
        var db = new SqliteDatabaseService(NullLogger<SqliteDatabaseService>.Instance, dbPath);

        // Attempt SQL injection via user note title and content
        string injectionTitle = "'; DROP TABLE Notes; --";
        string injectionContent = "' OR 1=1; --";

        var note = new NoteItem
        {
            Title = injectionTitle,
            Content = injectionContent,
            IsPinned = false
        };

        await db.SaveNoteAsync(note);

        // Retrieve notes to ensure table is intact and title was preserved literally
        var allNotes = await db.GetNotesAsync();
        Assert.Single(allNotes);
        Assert.Equal(injectionTitle, allNotes[0].Title);
        Assert.Equal(injectionContent, allNotes[0].Content);

        // Table still exists and can be queried safely
        var countNotes = await db.GetNotesAsync();
        Assert.NotEmpty(countNotes);
    }


}
