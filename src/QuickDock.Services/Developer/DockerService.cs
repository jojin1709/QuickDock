using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;

namespace QuickDock.Services.Developer;

public sealed class DockerService : IDockerService
{
    private readonly ILogger<DockerService> _logger;
    private string? _cachedDockerPath;

    public DockerService(ILogger<DockerService> logger)
    {
        _logger = logger;
    }

    private string? ResolveDockerPath()
    {
        if (!string.IsNullOrEmpty(_cachedDockerPath) && File.Exists(_cachedDockerPath))
            return _cachedDockerPath;

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in paths)
        {
            try
            {
                var candidate = Path.Combine(p.Trim('"', ' '), "docker.exe");
                if (File.Exists(candidate))
                {
                    _cachedDockerPath = candidate;
                    return candidate;
                }
            }
            catch
            {
                // ignore invalid paths
            }
        }

        string[] standardLocations =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Docker", "Docker", "resources", "bin", "docker.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Docker", "Docker", "resources", "docker.exe")
        ];

        foreach (var loc in standardLocations)
        {
            if (File.Exists(loc))
            {
                _cachedDockerPath = loc;
                return loc;
            }
        }

        return null;
    }

    public async Task<DockerStatusInfo> GetDockerStatusAsync(CancellationToken cancellationToken = default)
    {
        var dockerPath = ResolveDockerPath();
        if (string.IsNullOrEmpty(dockerPath))
        {
            return new DockerStatusInfo
            {
                IsAvailable = false,
                EngineState = "Unavailable",
                StatusMessage = "Docker CLI is not installed or could not be found."
            };
        }

        // Check version and daemon connection: docker version --format "{{json .}}"
        var versionResult = await RunDockerCommandAsync(["version", "--format", "{{json .}}"], cancellationToken);
        if (!versionResult.Success)
        {
            // If docker command executed but failed with daemon connection error:
            bool isDaemonDown = versionResult.ErrorOutput.Contains("daemon is not running", StringComparison.OrdinalIgnoreCase) ||
                                versionResult.ErrorOutput.Contains("error during connect", StringComparison.OrdinalIgnoreCase) ||
                                versionResult.Output.Contains("error during connect", StringComparison.OrdinalIgnoreCase);

            return new DockerStatusInfo
            {
                IsAvailable = true,
                EngineState = isDaemonDown ? "Stopped" : "Unavailable",
                Version = "Installed",
                StatusMessage = isDaemonDown
                    ? "Docker Desktop is not running. Please start the Docker daemon."
                    : (string.IsNullOrWhiteSpace(versionResult.ErrorOutput) ? "Failed to query Docker daemon." : versionResult.ErrorOutput.Trim())
            };
        }

        string clientVer = "Unknown";
        string engineState = "Running";
        try
        {
            using var doc = JsonDocument.Parse(versionResult.Output);
            if (doc.RootElement.TryGetProperty("Client", out var clientElem) &&
                clientElem.TryGetProperty("Version", out var verElem))
            {
                clientVer = verElem.GetString() ?? "Unknown";
            }
        }
        catch
        {
            clientVer = "Docker CLI";
        }

        // Count containers and images
        var containers = await GetContainersAsync(true, cancellationToken);
        var images = await GetImagesAsync(cancellationToken);

        int running = 0;
        foreach (var c in containers)
        {
            if (c.IsRunning) running++;
        }

        return new DockerStatusInfo
        {
            IsAvailable = true,
            Version = clientVer,
            EngineState = engineState,
            ContainersRunning = running,
            ContainersTotal = containers.Count,
            ImagesCount = images.Count,
            StatusMessage = $"Docker Engine {clientVer} is active."
        };
    }

    public async Task<IReadOnlyList<DockerContainerInfo>> GetContainersAsync(bool all = true, CancellationToken cancellationToken = default)
    {
        var dockerPath = ResolveDockerPath();
        if (string.IsNullOrEmpty(dockerPath)) return [];

        List<string> args = ["ps", "--format", "{{json .}}"];
        if (all) args.Add("-a");

        var result = await RunDockerCommandAsync(args.ToArray(), cancellationToken);
        if (!result.Success || string.IsNullOrWhiteSpace(result.Output)) return [];

        var list = new List<DockerContainerInfo>();
        var lines = result.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                string id = root.TryGetProperty("ID", out var idProp) ? idProp.GetString() ?? "" : "";
                string names = root.TryGetProperty("Names", out var nameProp) ? nameProp.GetString() ?? "" : "";
                string image = root.TryGetProperty("Image", out var imgProp) ? imgProp.GetString() ?? "" : "";
                string state = root.TryGetProperty("State", out var stateProp) ? stateProp.GetString() ?? "" : "";
                string status = root.TryGetProperty("Status", out var stProp) ? stProp.GetString() ?? "" : "";
                string ports = root.TryGetProperty("Ports", out var portProp) ? portProp.GetString() ?? "" : "";
                string created = root.TryGetProperty("CreatedAt", out var crProp) ? crProp.GetString() ?? "" : "";
                string command = root.TryGetProperty("Command", out var cmdProp) ? cmdProp.GetString() ?? "" : "";

                if (!string.IsNullOrEmpty(id))
                {
                    list.Add(new DockerContainerInfo
                    {
                        Id = id,
                        Names = names,
                        Image = image,
                        State = state,
                        Status = status,
                        Ports = ports,
                        Created = created,
                        Command = command
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse Docker container line: {Line}", line);
            }
        }

        return list;
    }

    public async Task<IReadOnlyList<DockerImageInfo>> GetImagesAsync(CancellationToken cancellationToken = default)
    {
        var dockerPath = ResolveDockerPath();
        if (string.IsNullOrEmpty(dockerPath)) return [];

        var result = await RunDockerCommandAsync(["images", "--format", "{{json .}}"], cancellationToken);
        if (!result.Success || string.IsNullOrWhiteSpace(result.Output)) return [];

        var list = new List<DockerImageInfo>();
        var lines = result.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                string id = root.TryGetProperty("ID", out var idProp) ? idProp.GetString() ?? "" : "";
                string repo = root.TryGetProperty("Repository", out var rProp) ? rProp.GetString() ?? "" : "";
                string tag = root.TryGetProperty("Tag", out var tProp) ? tProp.GetString() ?? "" : "";
                string size = root.TryGetProperty("Size", out var sProp) ? sProp.GetString() ?? "" : "";
                string created = root.TryGetProperty("CreatedAt", out var cProp) ? cProp.GetString() ?? "" : "";

                if (!string.IsNullOrEmpty(id))
                {
                    list.Add(new DockerImageInfo
                    {
                        Id = id,
                        Repository = repo,
                        Tag = tag,
                        Size = size,
                        Created = created
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse Docker image line: {Line}", line);
            }
        }

        return list;
    }

    public Task<DockerCommandResult> StartContainerAsync(string containerId, CancellationToken cancellationToken = default) =>
        RunDockerCommandAsync(["start", containerId], cancellationToken);

    public Task<DockerCommandResult> StopContainerAsync(string containerId, CancellationToken cancellationToken = default) =>
        RunDockerCommandAsync(["stop", containerId], cancellationToken);

    public Task<DockerCommandResult> RestartContainerAsync(string containerId, CancellationToken cancellationToken = default) =>
        RunDockerCommandAsync(["restart", containerId], cancellationToken);

    public async Task<string> GetContainerLogsAsync(string containerId, int tailLines = 100, CancellationToken cancellationToken = default)
    {
        var result = await RunDockerCommandAsync(["logs", "--tail", tailLines.ToString(), containerId], cancellationToken);
        return string.IsNullOrEmpty(result.Output) ? result.ErrorOutput : result.Output;
    }

    public async Task<string> InspectContainerAsync(string containerId, CancellationToken cancellationToken = default)
    {
        var result = await RunDockerCommandAsync(["inspect", containerId], cancellationToken);
        return result.Output;
    }

    public Task<DockerCommandResult> RemoveContainerAsync(string containerId, bool force = false, CancellationToken cancellationToken = default)
    {
        string[] args = force ? ["rm", "-f", containerId] : ["rm", containerId];
        return RunDockerCommandAsync(args, cancellationToken);
    }

    public Task<DockerCommandResult> RemoveImageAsync(string imageId, bool force = false, CancellationToken cancellationToken = default)
    {
        string[] args = force ? ["rmi", "-f", imageId] : ["rmi", imageId];
        return RunDockerCommandAsync(args, cancellationToken);
    }

    private async Task<DockerCommandResult> RunDockerCommandAsync(string[] arguments, CancellationToken cancellationToken)
    {
        var dockerPath = ResolveDockerPath();
        if (string.IsNullOrEmpty(dockerPath))
        {
            return new DockerCommandResult
            {
                Success = false,
                ErrorOutput = "Docker CLI is not installed or could not be found."
            };
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = dockerPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var a in arguments)
        {
            startInfo.ArgumentList.Add(a);
        }

        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            string stdout = await stdoutTask;
            string stderr = await stderrTask;

            return new DockerCommandResult
            {
                Success = process.ExitCode == 0,
                Output = stdout,
                ErrorOutput = stderr
            };
        }
        catch (OperationCanceledException)
        {
            return new DockerCommandResult
            {
                Success = false,
                ErrorOutput = "Docker command canceled."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running Docker command: {Args}", string.Join(" ", arguments));
            return new DockerCommandResult
            {
                Success = false,
                ErrorOutput = ex.Message
            };
        }
    }
}
