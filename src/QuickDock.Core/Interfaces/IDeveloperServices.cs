using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using QuickDock.Core.Models;

namespace QuickDock.Core.Interfaces;

public interface IGitService
{
    Task<bool> IsGitInstalledAsync();
    Task<string> GetGitVersionAsync();
    Task<bool> IsGitRepositoryAsync(string directoryPath);
    Task<GitStatusResult> GetRepositoryStatusAsync(string directoryPath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitBranchInfo>> GetBranchesAsync(string directoryPath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitCommitInfo>> GetRecentCommitsAsync(string directoryPath, int maxCount = 20, CancellationToken cancellationToken = default);
    Task<string> GetDiffAsync(string directoryPath, string? filePath = null, CancellationToken cancellationToken = default);
    Task<GitCommandResult> StageFileAsync(string directoryPath, string relativePath, CancellationToken cancellationToken = default);
    Task<GitCommandResult> StageAllAsync(string directoryPath, CancellationToken cancellationToken = default);
    Task<GitCommandResult> UnstageFileAsync(string directoryPath, string relativePath, CancellationToken cancellationToken = default);
    Task<GitCommandResult> CommitAsync(string directoryPath, string message, CancellationToken cancellationToken = default);
    Task<GitCommandResult> FetchAsync(string directoryPath, CancellationToken cancellationToken = default);
    Task<GitCommandResult> PullAsync(string directoryPath, CancellationToken cancellationToken = default);
    Task<GitCommandResult> PushAsync(string directoryPath, CancellationToken cancellationToken = default);
    Task<GitCommandResult> DiscardFileChangesAsync(string directoryPath, string relativePath, CancellationToken cancellationToken = default);
}

public interface IDockerService
{
    Task<DockerStatusInfo> GetDockerStatusAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DockerContainerInfo>> GetContainersAsync(bool all = true, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DockerImageInfo>> GetImagesAsync(CancellationToken cancellationToken = default);
    Task<DockerCommandResult> StartContainerAsync(string containerId, CancellationToken cancellationToken = default);
    Task<DockerCommandResult> StopContainerAsync(string containerId, CancellationToken cancellationToken = default);
    Task<DockerCommandResult> RestartContainerAsync(string containerId, CancellationToken cancellationToken = default);
    Task<string> GetContainerLogsAsync(string containerId, int tailLines = 100, CancellationToken cancellationToken = default);
    Task<string> InspectContainerAsync(string containerId, CancellationToken cancellationToken = default);
    Task<DockerCommandResult> RemoveContainerAsync(string containerId, bool force = false, CancellationToken cancellationToken = default);
    Task<DockerCommandResult> RemoveImageAsync(string imageId, bool force = false, CancellationToken cancellationToken = default);
}

public interface ITerminalLauncherService
{
    bool CanLaunch(TerminalType type);
    bool LaunchTerminal(TerminalLaunchRequest request);
}

public interface IJsonToolkitService
{
    JsonFormatResult Format(string jsonInput, bool indented = true);
    JsonFormatResult Minify(string jsonInput);
    JsonFormatResult Validate(string jsonInput);
}

public interface IJwtToolkitService
{
    JwtTokenInfo Decode(string rawToken, string? verificationSecret = null);
}

public interface IEncodingToolkitService
{
    EncodingResult Base64Encode(string input);
    EncodingResult Base64Decode(string input);
    EncodingResult UrlEncode(string input);
    EncodingResult UrlDecode(string input);
    EncodingResult ConvertEncoding(string input, string fromEncoding, string toEncoding);
}

public interface IDeveloperHashService
{
    DeveloperHashResult HashText(string input, DeveloperHashAlgorithm algorithm);
    Task<DeveloperHashResult> HashFileAsync(string filePath, DeveloperHashAlgorithm algorithm, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
}

public interface IUuidToolkitService
{
    IReadOnlyList<string> Generate(UuidOptions options);
}

public interface ITimestampToolkitService
{
    TimestampConversionResult ConvertFromUnixSeconds(long seconds);
    TimestampConversionResult ConvertFromUnixMilliseconds(long milliseconds);
    TimestampConversionResult ConvertFromDateTime(DateTimeOffset dateTime);
    TimestampConversionResult GetCurrent();
}

public interface IRegexToolkitService
{
    RegexTestResult TestRegex(string pattern, string input, RegexOptions options, string? replacementPattern = null, TimeSpan? timeout = null);
}

public interface IQrGeneratorService
{
    QrGenerationResult GenerateQr(string content, int pixelsPerModule = 10);
    QrGenerationResult GenerateWiFiQr(string ssid, string password, string authType);
}

public interface IHttpToolkitService
{
    Task<HttpResponseModel> SendRequestAsync(HttpRequestModel request, CancellationToken cancellationToken = default);
}

public interface ILocalDevelopmentService
{
    Task<IReadOnlyList<ListeningPortInfo>> GetListeningPortsAsync(CancellationToken cancellationToken = default);
}

public interface IDeveloperProcessService
{
    Task<IReadOnlyList<DeveloperProcessInfo>> GetDeveloperProcessesAsync(CancellationToken cancellationToken = default);
    bool TerminateProcess(int processId);
}

