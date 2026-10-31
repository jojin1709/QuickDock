using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;

namespace QuickDock.Services.Developer;

public sealed class RegexToolkitService : IRegexToolkitService
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(1.5);

    public RegexTestResult TestRegex(
        string pattern,
        string input,
        RegexOptions options,
        string? replacementPattern = null,
        TimeSpan? timeout = null)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return new RegexTestResult
            {
                Success = false,
                ErrorMessage = "Pattern cannot be empty."
            };
        }

        var matchTimeout = timeout ?? DefaultTimeout;
        var sw = Stopwatch.StartNew();

        try
        {
            var regex = new Regex(pattern, options, matchTimeout);
            var matchDetails = new List<RegexMatchDetail>();

            var matches = regex.Matches(input ?? string.Empty);
            foreach (Match match in matches)
            {
                var groupDetails = new List<RegexGroupDetail>();
                foreach (Group group in match.Groups)
                {
                    groupDetails.Add(new RegexGroupDetail
                    {
                        Name = group.Name,
                        Index = group.Index,
                        Length = group.Length,
                        Value = group.Value
                    });
                }

                matchDetails.Add(new RegexMatchDetail
                {
                    Index = match.Index,
                    Length = match.Length,
                    Value = match.Value,
                    Groups = groupDetails
                });
            }

            string replacementResult = string.Empty;
            if (replacementPattern != null)
            {
                replacementResult = regex.Replace(input ?? string.Empty, replacementPattern);
            }

            sw.Stop();

            return new RegexTestResult
            {
                Success = true,
                Pattern = pattern,
                Matches = matchDetails,
                ReplacementResult = replacementResult,
                ExecutionTimeMs = sw.Elapsed.TotalMilliseconds
            };
        }
        catch (RegexMatchTimeoutException)
        {
            sw.Stop();
            return new RegexTestResult
            {
                Success = false,
                Pattern = pattern,
                IsTimedOut = true,
                ExecutionTimeMs = sw.Elapsed.TotalMilliseconds,
                ErrorMessage = $"Evaluation timed out after {matchTimeout.TotalSeconds} seconds (catastrophic backtracking protected)."
            };
        }
        catch (ArgumentException ex)
        {
            sw.Stop();
            return new RegexTestResult
            {
                Success = false,
                Pattern = pattern,
                ExecutionTimeMs = sw.Elapsed.TotalMilliseconds,
                ErrorMessage = $"Invalid regex syntax: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new RegexTestResult
            {
                Success = false,
                Pattern = pattern,
                ExecutionTimeMs = sw.Elapsed.TotalMilliseconds,
                ErrorMessage = ex.Message
            };
        }
    }
}
