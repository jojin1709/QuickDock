using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;

namespace QuickDock.Services.Developer;

public sealed class GitService : IGitService
{
    private readonly ILogger<GitService> _logger;
    private string? _cachedGitPath;

    public GitService(ILogger<GitService> logger)
    {
        _logger = logger;
    }

    private string? ResolveGitPath()
    {
        if (!string.IsNullOrEmpty(_cachedGitPath) && File.Exists(_cachedGitPath))
        {
            return _cachedGitPath;
        }

        // 1. Check PATH
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in paths)
        {
            try
            {
                var candidate = Path.Combine(p.Trim('"', ' '), "git.exe");
                if (File.Exists(candidate))
                {
                    _cachedGitPath = candidate;
                    return candidate;
                }
            }
            catch
            {
                // ignore invalid path entries
            }
        }

        // 2. Standard Program Files locations
        string[] standardLocations =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "cmd", "git.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "bin", "git.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Git", "cmd", "git.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Git", "cmd", "git.exe")
        ];

        foreach (var loc in standardLocations)
        {
            if (File.Exists(loc))
            {
                _cachedGitPath = loc;
                return loc;
            }
        }

        return null;
    }

    public async Task<bool> IsGitInstalledAsync()
    {
        var path = ResolveGitPath();
        if (string.IsNullOrEmpty(path)) return false;

        var result = await RunGitCommandAsync(null, ["--version"], CancellationToken.None);
        return result.Success;
    }

    public async Task<string> GetGitVersionAsync()
    {
        var path = ResolveGitPath();
        if (string.IsNullOrEmpty(path)) return "Git is not installed or could not be located.";

        var result = await RunGitCommandAsync(null, ["--version"], CancellationToken.None);
        return result.Success ? result.Output.Trim() : "Git version check failed.";
    }

    public async Task<bool> IsGitRepositoryAsync(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            return false;

        var result = await RunGitCommandAsync(directoryPath, ["rev-parse", "--is-inside-work-tree"], CancellationToken.None);
        return result.Success && result.Output.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<GitStatusResult> GetRepositoryStatusAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return new GitStatusResult
            {
                IsGitRepository = false,
                ErrorMessage = "Directory does not exist."
            };
        }

        bool isRepo = await IsGitRepositoryAsync(directoryPath);
        if (!isRepo)
        {
            return new GitStatusResult
            {
                IsGitRepository = false,
                RepositoryPath = directoryPath,
                ErrorMessage = "Not a git repository."
            };
        }

        // 1. git status --porcelain=v1 -b
        var statusCmd = await RunGitCommandAsync(directoryPath, ["status", "--porcelain=v1", "-b"], cancellationToken);
        if (!statusCmd.Success)
        {
            return new GitStatusResult
            {
                IsGitRepository = true,
                RepositoryPath = directoryPath,
                ErrorMessage = statusCmd.ErrorOutput
            };
        }

        string branchName = "HEAD (detached)";
        int ahead = 0;
        int behind = 0;
        var fileList = new List<GitFileStatus>();

        var lines = statusCmd.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.StartsWith("## "))
            {
                // Branch line, e.g. "## master...origin/master [ahead 1, behind 2]" or "## Initial commit on master"
                var branchHeader = line[3..].Trim();
                int dotsIndex = branchHeader.IndexOf("...", StringComparison.Ordinal);
                if (dotsIndex > 0)
                {
                    branchName = branchHeader[..dotsIndex].Trim();
                }
                else
                {
                    int spaceIndex = branchHeader.IndexOf(' ');
                    branchName = spaceIndex > 0 ? branchHeader[..spaceIndex].Trim() : branchHeader;
                }

                if (line.Contains("[ahead "))
                {
                    var m = global::System.Text.RegularExpressions.Regex.Match(line, @"ahead (\d+)");
                    if (m.Success && int.TryParse(m.Groups[1].Value, out int a)) ahead = a;
                }
                if (line.Contains("behind "))
                {
                    var m = global::System.Text.RegularExpressions.Regex.Match(line, @"behind (\d+)");
                    if (m.Success && int.TryParse(m.Groups[1].Value, out int b)) behind = b;
                }
            }
            else if (line.Length >= 4)
            {
                char x = line[0];
                char y = line[1];
                string filePath = line[3..].Trim();

                // If renamed: "R  old -> new"
                if (filePath.Contains(" -> "))
                {
                    var parts = filePath.Split(" -> ");
                    if (parts.Length > 1) filePath = parts[1];
                }

                if (x == '?' && y == '?')
                {
                    fileList.Add(new GitFileStatus { Path = filePath, Status = GitFileStatusType.Untracked, IsStaged = false });
                }
                else
                {
                    // Check staged status (x column)
                    if (x != ' ' && x != '?')
                    {
                        var status = x switch
                        {
                            'M' => GitFileStatusType.Modified,
                            'A' => GitFileStatusType.Added,
                            'D' => GitFileStatusType.Deleted,
                            'R' => GitFileStatusType.Renamed,
                            _ => GitFileStatusType.Staged
                        };
                        fileList.Add(new GitFileStatus { Path = filePath, Status = status, IsStaged = true });
                    }

                    // Check unstaged status (y column)
                    if (y != ' ' && y != '?')
                    {
                        var status = y switch
                        {
                            'M' => GitFileStatusType.Modified,
                            'D' => GitFileStatusType.Deleted,
                            'A' => GitFileStatusType.Added,
                            _ => GitFileStatusType.Modified
                        };
                        fileList.Add(new GitFileStatus { Path = filePath, Status = status, IsStaged = false });
                    }
                }
            }
        }

        // 2. Head commit info: git log -1 --format="%H%x1f%s"
        string headHash = string.Empty;
        string headMsg = string.Empty;
        var logCmd = await RunGitCommandAsync(directoryPath, ["log", "-1", "--format=%H%x1f%s"], cancellationToken);
        if (logCmd.Success && !string.IsNullOrWhiteSpace(logCmd.Output))
        {
            var parts = logCmd.Output.Trim().Split('\x1f');
            if (parts.Length > 0) headHash = parts[0];
            if (parts.Length > 1) headMsg = parts[1];
        }

        return new GitStatusResult
        {
            IsGitRepository = true,
            RepositoryPath = directoryPath,
            CurrentBranch = branchName,
            HeadCommitHash = headHash,
            HeadCommitMessage = headMsg,
            Files = fileList,
            AheadCount = ahead,
            BehindCount = behind
        };
    }

    public async Task<IReadOnlyList<GitBranchInfo>> GetBranchesAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        var result = await RunGitCommandAsync(directoryPath, ["branch", "-a", "--format=%(refname:short)%x1f%(HEAD)"], cancellationToken);
        if (!result.Success) return [];

        var branches = new List<GitBranchInfo>();
        var lines = result.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var parts = line.Split('\x1f');
            string name = parts[0].Trim();
            bool isCurrent = parts.Length > 1 && parts[1].Trim() == "*";
            bool isRemote = name.StartsWith("origin/") || name.StartsWith("remotes/");
            branches.Add(new GitBranchInfo
            {
                Name = name,
                IsCurrent = isCurrent,
                IsRemote = isRemote
            });
        }
        return branches;
    }

    public async Task<IReadOnlyList<GitCommitInfo>> GetRecentCommitsAsync(string directoryPath, int maxCount = 20, CancellationToken cancellationToken = default)
    {
        var result = await RunGitCommandAsync(directoryPath, ["log", $"-n{maxCount}", "--format=%H%x1f%h%x1f%an%x1f%ae%x1f%aI%x1f%s"], cancellationToken);
        if (!result.Success) return [];

        var commits = new List<GitCommitInfo>();
        var lines = result.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var parts = line.Split('\x1f');
            if (parts.Length >= 6)
            {
                DateTimeOffset.TryParse(parts[4], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date);
                commits.Add(new GitCommitInfo
                {
                    Hash = parts[0],
                    ShortHash = parts[1],
                    AuthorName = parts[2],
                    AuthorEmail = parts[3],
                    CommitDate = date,
                    Message = parts[5]
                });
            }
        }
        return commits;
    }

    public async Task<string> GetDiffAsync(string directoryPath, string? filePath = null, CancellationToken cancellationToken = default)
    {
        string[] args = string.IsNullOrWhiteSpace(filePath)
            ? ["diff"]
            : ["diff", "--", filePath];

        var result = await RunGitCommandAsync(directoryPath, args, cancellationToken);
        return result.Output;
    }

    public Task<GitCommandResult> StageFileAsync(string directoryPath, string relativePath, CancellationToken cancellationToken = default) =>
        RunGitCommandAsync(directoryPath, ["add", "--", relativePath], cancellationToken);

    public Task<GitCommandResult> StageAllAsync(string directoryPath, CancellationToken cancellationToken = default) =>
        RunGitCommandAsync(directoryPath, ["add", "-A"], cancellationToken);

    public Task<GitCommandResult> UnstageFileAsync(string directoryPath, string relativePath, CancellationToken cancellationToken = default) =>
        RunGitCommandAsync(directoryPath, ["restore", "--staged", "--", relativePath], cancellationToken);

    public Task<GitCommandResult> CommitAsync(string directoryPath, string message, CancellationToken cancellationToken = default) =>
        RunGitCommandAsync(directoryPath, ["commit", "-m", message], cancellationToken);

    public Task<GitCommandResult> FetchAsync(string directoryPath, CancellationToken cancellationToken = default) =>
        RunGitCommandAsync(directoryPath, ["fetch"], cancellationToken);

    public Task<GitCommandResult> PullAsync(string directoryPath, CancellationToken cancellationToken = default) =>
        RunGitCommandAsync(directoryPath, ["pull"], cancellationToken);

    public Task<GitCommandResult> PushAsync(string directoryPath, CancellationToken cancellationToken = default) =>
        RunGitCommandAsync(directoryPath, ["push"], cancellationToken);

    public Task<GitCommandResult> DiscardFileChangesAsync(string directoryPath, string relativePath, CancellationToken cancellationToken = default) =>
        RunGitCommandAsync(directoryPath, ["restore", "--", relativePath], cancellationToken);

    private async Task<GitCommandResult> RunGitCommandAsync(string? workingDirectory, string[] arguments, CancellationToken cancellationToken)
    {
        var gitPath = ResolveGitPath();
        if (string.IsNullOrEmpty(gitPath))
        {
            return new GitCommandResult
            {
                Success = false,
                ErrorOutput = "Git is not installed or could not be located.",
                ExitCode = -1
            };
        }

        var runResult = await QuickDock.Core.Utilities.ProcessExecutionHelper.RunProcessAsync(
            gitPath,
            arguments,
            workingDirectory,
            TimeSpan.FromSeconds(30),
            cancellationToken).ConfigureAwait(false);

        return new GitCommandResult
        {
            Success = runResult.Success,
            Output = runResult.Output,
            ErrorOutput = runResult.ErrorOutput,
            ExitCode = runResult.ExitCode
        };
    }
}
