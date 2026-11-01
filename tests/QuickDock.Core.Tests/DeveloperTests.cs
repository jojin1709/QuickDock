using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;
using QuickDock.Core.Modules;
using QuickDock.Modules.Developer;
using QuickDock.Services.Developer;
using Xunit;

namespace QuickDock.Core.Tests;

public class DeveloperTests
{
    #region Git Tests

    [Fact]
    public async Task GitService_DetectsCurrentRepository()
    {
        var service = new GitService(NullLogger<GitService>.Instance);
        bool isInstalled = await service.IsGitInstalledAsync();

        if (isInstalled)
        {
            string version = await service.GetGitVersionAsync();
            Assert.Contains("git version", version, StringComparison.OrdinalIgnoreCase);

            // Solution root is a git repository
            string currentDir = Directory.GetCurrentDirectory();
            bool isRepo = await service.IsGitRepositoryAsync(currentDir);
            Assert.True(isRepo);

            var status = await service.GetRepositoryStatusAsync(currentDir);
            Assert.True(status.IsGitRepository);
            Assert.False(string.IsNullOrEmpty(status.CurrentBranch));
        }
    }

    [Fact]
    public async Task GitService_NonGitDirectory_ReturnsSafeStatus()
    {
        var service = new GitService(NullLogger<GitService>.Instance);
        string tempDir = Path.Combine(Path.GetTempPath(), "quickdock_non_git_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            bool isRepo = await service.IsGitRepositoryAsync(tempDir);
            Assert.False(isRepo);

            var status = await service.GetRepositoryStatusAsync(tempDir);
            Assert.False(status.IsGitRepository);
            Assert.Contains("Not a git repository", status.ErrorMessage);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task GitService_GetBranchesAndCommits_ExecutesSafely()
    {
        var service = new GitService(NullLogger<GitService>.Instance);
        if (await service.IsGitInstalledAsync())
        {
            string currentDir = Directory.GetCurrentDirectory();
            var branches = await service.GetBranchesAsync(currentDir);
            Assert.NotNull(branches);
            Assert.NotEmpty(branches);

            var commits = await service.GetRecentCommitsAsync(currentDir, 5);
            Assert.NotNull(commits);
            Assert.NotEmpty(commits);
            Assert.False(string.IsNullOrEmpty(commits[0].Hash));
        }
    }

    #endregion

    #region Docker Tests

    [Fact]
    public async Task DockerService_GetStatus_ReturnsSafeResponse()
    {
        var service = new DockerService(NullLogger<DockerService>.Instance);
        var status = await service.GetDockerStatusAsync();
        Assert.NotNull(status);
        Assert.False(string.IsNullOrEmpty(status.EngineState));
        Assert.False(string.IsNullOrEmpty(status.StatusMessage));
    }

    [Fact]
    public async Task DockerService_ContainersAndImages_HandlesUnavailableGracefully()
    {
        var service = new DockerService(NullLogger<DockerService>.Instance);
        var containers = await service.GetContainersAsync(true);
        var images = await service.GetImagesAsync();
        Assert.NotNull(containers);
        Assert.NotNull(images);
    }

    #endregion

    #region Terminal Tests

    [Fact]
    public void TerminalLauncher_CanLaunch_ReturnsExpectedForInstalledTerminals()
    {
        var service = new TerminalLauncherService(NullLogger<TerminalLauncherService>.Instance);
        bool canCmd = service.CanLaunch(TerminalType.CommandPrompt);
        Assert.True(canCmd); // cmd.exe is always present on Windows
    }

    #endregion

    #region JSON Tests

    [Fact]
    public void JsonToolkit_Format_ValidJson_FormatsWithIndentation()
    {
        var service = new JsonToolkitService();
        string raw = "{\"id\":1,\"name\":\"QuickDock\",\"active\":true}";
        var res = service.Format(raw, indented: true);

        Assert.True(res.Success);
        Assert.Contains("\n", res.FormattedText);
        Assert.Contains("  \"name\": \"QuickDock\"", res.FormattedText);
    }

    [Fact]
    public void JsonToolkit_Minify_ValidJson_RemovesWhitespace()
    {
        var service = new JsonToolkitService();
        string raw = "{\n  \"id\": 1,\n  \"name\": \"QuickDock\"\n}";
        var res = service.Minify(raw);

        Assert.True(res.Success);
        Assert.DoesNotContain("\n", res.FormattedText);
        Assert.Equal("{\"id\":1,\"name\":\"QuickDock\"}", res.FormattedText);
    }

    [Fact]
    public void JsonToolkit_Validate_InvalidJson_ReportsErrorLocation()
    {
        var service = new JsonToolkitService();
        string broken = "{\"name\": \"QuickDock\", broken}";
        var res = service.Validate(broken);

        Assert.False(res.Success);
        Assert.NotNull(res.ErrorMessage);
        Assert.True(res.ErrorLine > 0 || res.ErrorColumn > 0);
    }

    [Fact]
    public void JsonToolkit_Format_LargeJson_CompletesWithoutErrors()
    {
        var service = new JsonToolkitService();
        var sb = new StringBuilder();
        sb.Append('[');
        for (int i = 0; i < 500; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append($"{{\"id\":{i},\"value\":\"item_{i}\"}}");
        }
        sb.Append(']');

        var res = service.Format(sb.ToString(), indented: true);
        Assert.True(res.Success);
        Assert.Contains("item_499", res.FormattedText);
    }

    #endregion

    #region JWT Tests

    [Fact]
    public void JwtToolkit_Decode_ValidToken_DecodesHeaderPayloadAndClaims()
    {
        var service = new JwtToolkitService();
        // Standard JWT: header {"alg":"HS256","typ":"JWT"}, payload {"sub":"1234567890","name":"Jojin","iat":1516239022}
        string token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvamluIiwiaWF0IjoxNTE2MjM5MDIyfQ.XbPfbIHMI6arZ3Y922BhjWgQzWXcXNrz0ogrVugoGQ8";

        var res = service.Decode(token);

        Assert.True(res.IsDecoded);
        Assert.False(res.IsVerified); // Signature not verified without secret
        Assert.Equal("HS256", res.Algorithm);
        Assert.Equal("JWT", res.Type);
        Assert.Equal("1234567890", res.Subject);
        Assert.Equal("Jojin", res.Claims["name"]);
        Assert.Contains("Decoding a JWT does not verify its signature", res.VerificationMessage);
    }

    [Fact]
    public void JwtToolkit_Verify_WithValidHmacSecret_VerifiesSignature()
    {
        var service = new JwtToolkitService();
        // Generated with secret "quickdock-secret-key-12345"
        // header: {"alg":"HS256","typ":"JWT"} -> eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9
        // payload: {"sub":"jojin"} -> eyJzdWIiOiJqb2ppbiJ9
        string secret = "quickdock-secret-key-12345";
        string headerB64 = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9";
        string payloadB64 = "eyJzdWIiOiJqb2ppbiJ9";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        byte[] sigBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{headerB64}.{payloadB64}"));
        string sigB64 = Convert.ToBase64String(sigBytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        string fullToken = $"{headerB64}.{payloadB64}.{sigB64}";

        var res = service.Decode(fullToken, secret);
        Assert.True(res.IsDecoded);
        Assert.True(res.IsVerified);
        Assert.Contains("VERIFIED successfully", res.VerificationMessage);

        // Verification with wrong secret
        var failRes = service.Decode(fullToken, "wrong-secret");
        Assert.True(failRes.IsDecoded);
        Assert.False(failRes.IsVerified);
        Assert.Contains("verification FAILED", failRes.VerificationMessage);
    }

    [Fact]
    public void JwtToolkit_Decode_InvalidToken_HandlesGracefully()
    {
        var service = new JwtToolkitService();
        var res = service.Decode("not.a.valid.jwt.token");
        Assert.False(res.IsDecoded);
        Assert.Contains("Invalid JWT format", res.VerificationMessage);
    }

    #endregion

    #region Encoding Tests

    [Fact]
    public void EncodingToolkit_Base64_RoundTrip()
    {
        var service = new EncodingToolkitService();
        string original = "QuickDock Windows 11 Utility!";
        var enc = service.Base64Encode(original);
        Assert.True(enc.Success);

        var dec = service.Base64Decode(enc.OutputText);
        Assert.True(dec.Success);
        Assert.Equal(original, dec.OutputText);
    }

    [Fact]
    public void EncodingToolkit_Base64Decode_InvalidInput_ReturnsErrorWithoutException()
    {
        var service = new EncodingToolkitService();
        var res = service.Base64Decode("!!!NotValidBase64@@@");
        Assert.False(res.Success);
        Assert.Contains("Invalid Base64", res.ErrorMessage);
    }

    [Fact]
    public void EncodingToolkit_UrlEncode_RoundTrip()
    {
        var service = new EncodingToolkitService();
        string original = "https://example.com/search?q=QuickDock & Windows 11=yes";
        var enc = service.UrlEncode(original);
        Assert.True(enc.Success);
        Assert.Contains("%20", enc.OutputText);

        var dec = service.UrlDecode(enc.OutputText);
        Assert.True(dec.Success);
        Assert.Equal(original, dec.OutputText);
    }

    #endregion

    #region Hash Tests

    [Fact]
    public void HashToolkit_KnownSha256_ComputesCorrectDigest()
    {
        var service = new DeveloperHashService();
        // SHA-256("QuickDock") = 9070db79f6eb7e4ef4ee30d322fb753d6ebefee241b777a83d47adcf511e63a1
        var res = service.HashText("QuickDock", DeveloperHashAlgorithm.SHA256);
        Assert.True(res.HexHash.Length == 64);

        using var sha = SHA256.Create();
        string expected = Convert.ToHexStringLower(sha.ComputeHash(Encoding.UTF8.GetBytes("QuickDock")));
        Assert.Equal(expected, res.HexHash);
    }

    [Fact]
    public void HashToolkit_AllAlgorithms_ComputeExpectedLength()
    {
        var service = new DeveloperHashService();
        string input = "QuickDock Developer Toolkit";

        Assert.Equal(32, service.HashText(input, DeveloperHashAlgorithm.MD5).HexHash.Length);
        Assert.Equal(40, service.HashText(input, DeveloperHashAlgorithm.SHA1).HexHash.Length);
        Assert.Equal(64, service.HashText(input, DeveloperHashAlgorithm.SHA256).HexHash.Length);
        Assert.Equal(96, service.HashText(input, DeveloperHashAlgorithm.SHA384).HexHash.Length);
        Assert.Equal(128, service.HashText(input, DeveloperHashAlgorithm.SHA512).HexHash.Length);
    }

    [Fact]
    public async Task HashToolkit_StreamingFileHash_ComputesAccurateDigest()
    {
        var service = new DeveloperHashService();
        string tempFile = Path.GetTempFileName();
        try
        {
            byte[] data = new byte[128 * 1024]; // 128 KB
            new Random(42).NextBytes(data);
            await File.WriteAllBytesAsync(tempFile, data);

            double lastProgress = 0;
            var progress = new SynchronousProgress<double>(p => lastProgress = p);

            var res = await service.HashFileAsync(tempFile, DeveloperHashAlgorithm.SHA256, progress);

            using var sha = SHA256.Create();
            string expected = Convert.ToHexStringLower(sha.ComputeHash(data));

            Assert.Equal(expected, res.HexHash);
            Assert.True(lastProgress >= 0.99);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    private sealed class SynchronousProgress<T>(Action<T> action) : IProgress<T>
    {
        public void Report(T value) => action(value);
    }

    #endregion

    #region UUID Tests

    [Fact]
    public void UuidToolkit_Generate_ReturnsValidGuids()
    {
        var service = new UuidToolkitService();
        var list = service.Generate(new UuidOptions { Count = 10, Hyphens = true, Uppercase = false });

        Assert.Equal(10, list.Count);
        var set = new HashSet<string>(list);
        Assert.Equal(10, set.Count); // Uniqueness

        foreach (var guidStr in list)
        {
            Assert.True(Guid.TryParse(guidStr, out _));
            Assert.Contains("-", guidStr);
        }
    }

    [Fact]
    public void UuidToolkit_Generate_FormatsCorrectly()
    {
        var service = new UuidToolkitService();

        var braces = service.Generate(new UuidOptions { Count = 1, Braces = true, Uppercase = true })[0];
        Assert.StartsWith("{", braces);
        Assert.EndsWith("}", braces);
        Assert.Equal(braces, braces.ToUpperInvariant());

        var noHyphens = service.Generate(new UuidOptions { Count = 1, Hyphens = false })[0];
        Assert.Equal(32, noHyphens.Length);
        Assert.DoesNotContain("-", noHyphens);
    }

    #endregion

    #region Timestamp Tests

    [Fact]
    public void TimestampToolkit_SecondsAndMsConversion()
    {
        var service = new TimestampToolkitService();
        long epochSec = 1700000000;
        var resSec = service.ConvertFromUnixSeconds(epochSec);
        Assert.Equal(epochSec, resSec.UnixSeconds);
        Assert.False(string.IsNullOrEmpty(resSec.FormattedIso));
        Assert.False(string.IsNullOrEmpty(resSec.RelativeDescription));

        long epochMs = 1700000000000;
        var resMs = service.ConvertFromUnixMilliseconds(epochMs);
        Assert.Equal(epochMs, resMs.UnixMilliseconds);
    }

    #endregion

    #region Regex Tests

    [Fact]
    public void RegexToolkit_TestRegex_FindsMatchesAndCaptures()
    {
        var service = new RegexToolkitService();
        string pattern = @"(?<word>\w+)@(?<domain>\w+\.\w+)";
        string input = "Contact user@example.com or admin@test.org today.";

        var res = service.TestRegex(pattern, input, RegexOptions.IgnoreCase, "REPLACED");

        Assert.True(res.Success);
        Assert.Equal(2, res.MatchCount);
        Assert.Equal("user@example.com", res.Matches[0].Value);
        Assert.Equal("admin@test.org", res.Matches[1].Value);
        Assert.Equal("Contact REPLACED or REPLACED today.", res.ReplacementResult);
    }

    [Fact]
    public void RegexToolkit_CatastrophicPattern_ProtectsWithTimeout()
    {
        var service = new RegexToolkitService();
        // Classic ReDoS pattern
        string pattern = @"(a+)+$";
        string input = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaX";

        var res = service.TestRegex(pattern, input, RegexOptions.None, timeout: TimeSpan.FromMilliseconds(200));

        Assert.False(res.Success);
        Assert.True(res.IsTimedOut);
        Assert.Contains("timed out", res.ErrorMessage);
    }

    #endregion

    #region QR Tests

    [Fact]
    public void QrGenerator_GenerateQr_CreatesValidPngBytes()
    {
        var service = new QrGeneratorService();
        var res = service.GenerateQr("https://github.com/jojin1709/QuickDock");

        Assert.True(res.Success);
        Assert.NotNull(res.PngBytes);
        Assert.True(res.PngBytes.Length > 100);
        // PNG magic header: 0x89, 'P', 'N', 'G'
        Assert.Equal(0x89, res.PngBytes[0]);
        Assert.Equal((byte)'P', res.PngBytes[1]);
        Assert.Equal((byte)'N', res.PngBytes[2]);
        Assert.Equal((byte)'G', res.PngBytes[3]);
    }

    [Fact]
    public void QrGenerator_GenerateWiFiQr_FormatsCorrectPayload()
    {
        var service = new QrGeneratorService();
        var res = service.GenerateWiFiQr("MyHomeWifi", "SecretPass123", "WPA");

        Assert.True(res.Success);
        Assert.Contains("WIFI:S:MyHomeWifi;T:WPA;P:SecretPass123;;", res.RawPayload);
    }

    #endregion

    #region HTTP Tests

    [Fact]
    public async Task HttpToolkit_SendRequest_ExecutesAndFormatsJson()
    {
        var httpClient = new System.Net.Http.HttpClient();
        var service = new HttpToolkitService(httpClient, NullLogger<HttpToolkitService>.Instance);

        var req = new HttpRequestModel
        {
            Method = "GET",
            Url = "https://httpbin.org/get",
            Headers = new Dictionary<string, string> { ["X-Test-Header"] = "QuickDock" },
            TimeoutSeconds = 10
        };

        // Note: if internet access is blocked or restricted in test environment, it handles timeout/exception gracefully
        var res = await service.SendRequestAsync(req);
        Assert.NotNull(res);
        // Regardless of network availability, it never throws unhandled exception
        if (res.IsSuccess)
        {
            Assert.Equal(200, res.StatusCode);
            Assert.False(string.IsNullOrEmpty(res.Body));
        }
    }

    #endregion

    #region Local Dev & Processes Tests

    [Fact]
    public async Task LocalDevelopmentService_GetListeningPorts_ReturnsValidList()
    {
        var service = new LocalDevelopmentService(NullLogger<LocalDevelopmentService>.Instance);
        var ports = await service.GetListeningPortsAsync();
        Assert.NotNull(ports);
        // On a running Windows machine, there are typically listening ports (RPC, DNS, or apps)
        foreach (var p in ports)
        {
            Assert.True(p.Port > 0 && p.Port <= 65535);
            Assert.Equal("TCP", p.Protocol);
            Assert.StartsWith("http://localhost:", p.LocalhostUrl);
        }
    }

    [Fact]
    public async Task DeveloperProcessService_GetDeveloperProcesses_CorrelatesPorts()
    {
        var localDev = new LocalDevelopmentService(NullLogger<LocalDevelopmentService>.Instance);
        var procService = new DeveloperProcessService(localDev, NullLogger<DeveloperProcessService>.Instance);

        var processes = await procService.GetDeveloperProcessesAsync();
        Assert.NotNull(processes);
        Assert.NotEmpty(processes);

        // System protection test
        bool killSystem = procService.TerminateProcess(0); // System Idle
        Assert.False(killSystem);
        bool killInit = procService.TerminateProcess(4); // System PID 4
        Assert.False(killInit);
    }

    #endregion

    #region Module Lifecycle Tests

    [Fact]
    public async Task DeveloperModules_Lifecycle_AdheresToCoreRule()
    {
        var gitService = new GitService(NullLogger<GitService>.Instance);
        var dockerService = new DockerService(NullLogger<DockerService>.Instance);
        var termService = new TerminalLauncherService(NullLogger<TerminalLauncherService>.Instance);
        var httpService = new HttpToolkitService(new System.Net.Http.HttpClient(), NullLogger<HttpToolkitService>.Instance);
        var localDev = new LocalDevelopmentService(NullLogger<LocalDevelopmentService>.Instance);
        var procService = new DeveloperProcessService(localDev, NullLogger<DeveloperProcessService>.Instance);

        IQuickDockModule[] modules =
        [
            new GitModule(gitService, NullLogger<GitModule>.Instance),
            new DockerModule(dockerService, NullLogger<DockerModule>.Instance),
            new TerminalModule(termService, NullLogger<TerminalModule>.Instance),
            new DeveloperToolsModule(NullLogger<DeveloperToolsModule>.Instance),
            new HttpToolkitModule(httpService, NullLogger<HttpToolkitModule>.Instance),
            new LocalDevModule(localDev, NullLogger<LocalDevModule>.Instance),
            new ProcessMonitorModule(procService, NullLogger<ProcessMonitorModule>.Instance)
        ];

        foreach (var m in modules)
        {
            Assert.Equal(ModuleCategory.Developer, m.Category);
            Assert.Equal(ModuleState.Uninitialized, m.State);

            await m.InitializeAsync();
            Assert.Equal(ModuleState.Initialized, m.State);

            await m.StartAsync();
            Assert.Equal(ModuleState.Running, m.State);

            await m.StopAsync();
            Assert.Equal(ModuleState.Stopped, m.State);

            if (m is IDisposable d) d.Dispose();
        }
    }

    #endregion
}
