using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QuickDock.Core.Utilities;

/// <summary>
/// Result of an external process execution.
/// </summary>
public sealed record ProcessRunResult(
    bool Success,
    int ExitCode,
    string Output,
    string ErrorOutput);

/// <summary>
/// Safe process runner with timeout bounds, stream consumption, and guaranteed child process tree termination.
/// </summary>
public static class ProcessExecutionHelper
{
    /// <summary>
    /// Executes an external process asynchronously with bounded timeout and safe cleanup.
    /// </summary>
    public static async Task<ProcessRunResult> RunProcessAsync(
        string executable,
        IEnumerable<string> arguments,
        string? workingDirectory = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);

        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(30);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (effectiveTimeout > TimeSpan.Zero)
        {
            linkedCts.CancelAfter(effectiveTimeout);
        }

        var psi = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = !string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory)
                ? workingDirectory
                : Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = psi };

        try
        {
            if (!process.Start())
            {
                return new ProcessRunResult(false, -1, string.Empty, "Failed to start process.");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(linkedCts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(linkedCts.Token);

            await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);

            string stdout = await stdoutTask.ConfigureAwait(false);
            string stderr = await stderrTask.ConfigureAwait(false);

            return new ProcessRunResult(process.ExitCode == 0, process.ExitCode, stdout, stderr);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Process may have already exited
            }

            bool wasTimeout = !cancellationToken.IsCancellationRequested;
            return new ProcessRunResult(
                false,
                -2,
                string.Empty,
                wasTimeout ? $"Process timed out after {effectiveTimeout.TotalSeconds:0}s." : "Process execution canceled.");
        }
        catch (Exception ex)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch { }

            return new ProcessRunResult(false, -3, string.Empty, ex.Message);
        }
    }
}

