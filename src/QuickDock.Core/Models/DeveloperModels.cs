using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QuickDock.Core.Models;

#region Git Models

public enum GitFileStatusType
{
    Modified,
    Added,
    Deleted,
    Renamed,
    Untracked,
    Staged
}

public sealed class GitFileStatus
{
    public required string Path { get; init; }
    public GitFileStatusType Status { get; init; }
    public bool IsStaged { get; init; }

    public string StatusBadge => Status switch
    {
        GitFileStatusType.Modified => "M",
        GitFileStatusType.Added => "A",
        GitFileStatusType.Deleted => "D",
        GitFileStatusType.Renamed => "R",
        GitFileStatusType.Untracked => "?",
        GitFileStatusType.Staged => "S",
        _ => "•"
    };
}

public sealed class GitCommitInfo
{
    public required string Hash { get; init; }
    public required string ShortHash { get; init; }
    public required string AuthorName { get; init; }
    public string AuthorEmail { get; init; } = string.Empty;
    public DateTimeOffset CommitDate { get; init; }
    public required string Message { get; init; }
}

public sealed class GitBranchInfo
{
    public required string Name { get; init; }
    public bool IsCurrent { get; init; }
    public bool IsRemote { get; init; }
}

public sealed class GitStatusResult
{
    public bool IsGitRepository { get; init; }
    public string RepositoryPath { get; init; } = string.Empty;
    public string CurrentBranch { get; init; } = string.Empty;
    public string HeadCommitHash { get; init; } = string.Empty;
    public string HeadCommitMessage { get; init; } = string.Empty;
    public List<GitFileStatus> Files { get; init; } = [];
    public int AheadCount { get; init; }
    public int BehindCount { get; init; }
    public bool IsClean => Files.Count == 0;
    public string ErrorMessage { get; init; } = string.Empty;
}

public sealed class GitCommandResult
{
    public bool Success { get; init; }
    public string Output { get; init; } = string.Empty;
    public string ErrorOutput { get; init; } = string.Empty;
    public int ExitCode { get; init; }
}

#endregion

#region Docker Models

public sealed class DockerStatusInfo
{
    public bool IsAvailable { get; init; }
    public string Version { get; init; } = string.Empty;
    public string EngineState { get; init; } = "Unavailable"; // "Running", "Stopped", "Unavailable"
    public int ContainersRunning { get; init; }
    public int ContainersTotal { get; init; }
    public int ImagesCount { get; init; }
    public string StatusMessage { get; init; } = string.Empty;
}

public sealed class DockerContainerInfo
{
    public required string Id { get; init; }
    public string ShortId => Id.Length > 12 ? Id[..12] : Id;
    public required string Names { get; init; }
    public required string Image { get; init; }
    public required string State { get; init; } // "running", "exited", "paused"
    public string Status { get; init; } = string.Empty;
    public string Ports { get; init; } = string.Empty;
    public string Created { get; init; } = string.Empty;
    public string Command { get; init; } = string.Empty;
    public bool IsRunning => string.Equals(State, "running", StringComparison.OrdinalIgnoreCase);
}

public sealed class DockerImageInfo
{
    public required string Id { get; init; }
    public string ShortId => Id.Length > 12 ? Id[..12] : Id;
    public required string Repository { get; init; }
    public string Tag { get; init; } = "latest";
    public string Size { get; init; } = string.Empty;
    public string Created { get; init; } = string.Empty;
    public string FullName => $"{Repository}:{Tag}";
}

public sealed class DockerCommandResult
{
    public bool Success { get; init; }
    public string Output { get; init; } = string.Empty;
    public string ErrorOutput { get; init; } = string.Empty;
}

#endregion

#region Terminal Models

public enum TerminalType
{
    WindowsTerminal,
    PowerShell,
    CommandPrompt
}

public sealed class TerminalLaunchRequest
{
    public TerminalType Type { get; init; } = TerminalType.WindowsTerminal;
    public string WorkingDirectory { get; init; } = string.Empty;
    public string InitialCommand { get; init; } = string.Empty;
    public bool AsAdministrator { get; init; }
}

#endregion

#region JSON & JWT Models

public sealed class JsonFormatResult
{
    public bool Success { get; init; }
    public string FormattedText { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    public long? ErrorLine { get; init; }
    public long? ErrorColumn { get; init; }
}

public sealed class JwtTokenInfo
{
    public string RawToken { get; init; } = string.Empty;
    public string Algorithm { get; init; } = "Unknown";
    public string Type { get; init; } = "JWT";
    public string? Subject { get; init; }
    public string? Issuer { get; init; }
    public string? Audience { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public DateTimeOffset? NotBefore { get; init; }
    public DateTimeOffset? IssuedAt { get; init; }
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTimeOffset.UtcNow;
    public Dictionary<string, string> Claims { get; init; } = [];
    public string HeaderJson { get; init; } = string.Empty;
    public string PayloadJson { get; init; } = string.Empty;
    public bool IsDecoded { get; init; }
    public bool IsVerified { get; init; }
    public string? VerificationMessage { get; init; }
    public string SignatureHex { get; init; } = string.Empty;
}

#endregion

#region Encoding Models

public enum EncodingType
{
    Base64,
    Url,
    Utf8,
    Utf16,
    Hex
}

public sealed class EncodingResult
{
    public bool Success { get; init; }
    public string OutputText { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}

#endregion

#region Hash Models

public enum DeveloperHashAlgorithm
{
    MD5,
    SHA1,
    SHA256,
    SHA384,
    SHA512
}

public sealed class DeveloperHashResult
{
    public DeveloperHashAlgorithm Algorithm { get; init; }
    public string HexHash { get; init; } = string.Empty;
    public long ElapsedMilliseconds { get; init; }
    public string Source { get; init; } = string.Empty;
}

#endregion

#region UUID Models

public sealed class UuidOptions
{
    public int Count { get; init; } = 1;
    public bool Uppercase { get; init; }
    public bool Hyphens { get; init; } = true;
    public bool Braces { get; init; }
}

#endregion

#region Timestamp Models

public sealed class TimestampConversionResult
{
    public long UnixSeconds { get; init; }
    public long UnixMilliseconds { get; init; }
    public DateTimeOffset UtcDateTime { get; init; }
    public DateTimeOffset LocalDateTime { get; init; }
    public string FormattedIso { get; init; } = string.Empty;
    public string RelativeDescription { get; init; } = string.Empty;
}

#endregion

#region Regex Models

public sealed class RegexGroupDetail
{
    public required string Name { get; init; }
    public int Index { get; init; }
    public int Length { get; init; }
    public required string Value { get; init; }
}

public sealed class RegexMatchDetail
{
    public int Index { get; init; }
    public int Length { get; init; }
    public required string Value { get; init; }
    public List<RegexGroupDetail> Groups { get; init; } = [];
}

public sealed class RegexTestResult
{
    public bool Success { get; init; }
    public string Pattern { get; init; } = string.Empty;
    public int MatchCount => Matches.Count;
    public List<RegexMatchDetail> Matches { get; init; } = [];
    public string ReplacementResult { get; init; } = string.Empty;
    public double ExecutionTimeMs { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
    public bool IsTimedOut { get; init; }
}

#endregion

#region QR Models

public sealed class QrGenerationResult
{
    public bool Success { get; init; }
    public byte[]? PngBytes { get; init; }
    public string RawPayload { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}

#endregion

#region HTTP Models

public sealed class HttpRequestModel
{
    public string Method { get; init; } = "GET";
    public string Url { get; init; } = string.Empty;
    public Dictionary<string, string> Headers { get; init; } = [];
    public string Body { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/json";
    public int TimeoutSeconds { get; init; } = 30;
}

public sealed class HttpResponseModel
{
    public int StatusCode { get; init; }
    public string StatusText { get; init; } = string.Empty;
    public Dictionary<string, string> Headers { get; init; } = [];
    public string Body { get; init; } = string.Empty;
    public string FormattedBody { get; init; } = string.Empty;
    public long ElapsedMilliseconds { get; init; }
    public long ContentLengthBytes { get; init; }
    public bool IsSuccess => StatusCode >= 200 && StatusCode < 300;
    public string ErrorMessage { get; init; } = string.Empty;
}

#endregion

#region Local Dev & Process Models

public sealed class ListeningPortInfo
{
    public int Port { get; init; }
    public string Protocol { get; init; } = "TCP";
    public string LocalAddress { get; init; } = "127.0.0.1";
    public int ProcessId { get; init; }
    public string ProcessName { get; init; } = "Unknown";
    public string ProcessPath { get; init; } = string.Empty;
    public string LocalhostUrl => $"http://localhost:{Port}";
}

public sealed class DeveloperProcessInfo
{
    public int ProcessId { get; init; }
    public string ProcessName { get; init; } = string.Empty;
    public long WorkingSetBytes { get; init; }
    public double WorkingSetMb => Math.Round(WorkingSetBytes / (1024.0 * 1024.0), 1);
    public DateTime? StartTime { get; init; }
    public string MainModulePath { get; init; } = string.Empty;
    public List<int> ListeningPorts { get; init; } = [];
    public bool IsAccessible { get; init; } = true;
    public string PortsSummary => ListeningPorts.Count > 0 ? string.Join(", ", ListeningPorts) : "None";
}

#endregion
