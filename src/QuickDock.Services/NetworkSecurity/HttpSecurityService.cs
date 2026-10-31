using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;

namespace QuickDock.Services.NetworkSecurity;

public sealed class HttpSecurityService : IHttpSecurityService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpSecurityService> _logger;

    public HttpSecurityService(HttpClient httpClient, ILogger<HttpSecurityService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<HttpSecurityHeadersResult> InspectHeadersAsync(
        string url,
        int timeoutSeconds = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return new HttpSecurityHeadersResult
            {
                Success = false,
                TargetUrl = url,
                ErrorMessage = "URL cannot be empty."
            };
        }

        string targetUrl = url.Trim();
        if (!targetUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !targetUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            targetUrl = "https://" + targetUrl;
        }

        int safeTimeout = Math.Clamp(timeoutSeconds, 1, 60);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(safeTimeout));

        var sw = Stopwatch.StartNew();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, targetUrl);
            request.Headers.TryAddWithoutValidation("User-Agent", "QuickDock-Security-Inspector/1.0");

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            sw.Stop();

            var rawHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var h in response.Headers)
            {
                rawHeaders[h.Key] = string.Join("; ", h.Value);
            }
            foreach (var h in response.Content.Headers)
            {
                rawHeaders[h.Key] = string.Join("; ", h.Value);
            }

            var checks = new List<HeaderCheckItem>
            {
                EvaluateHeader(rawHeaders, "Strict-Transport-Security",
                    "Enforces encrypted HTTPS connections and mitigates protocol downgrade attacks.",
                    "Recommend setting 'max-age=31536000; includeSubDomains' on production HTTPS domains."),

                EvaluateHeader(rawHeaders, "Content-Security-Policy",
                    "Restricts resources (scripts, styles, images) that can be loaded to prevent XSS.",
                    "Define a strict CSP policy restricting script-src and default-src."),

                EvaluateHeader(rawHeaders, "X-Content-Type-Options",
                    "Prevents MIME-type sniffing by enforcing declared Content-Type.",
                    "Set to 'nosniff'."),

                EvaluateHeader(rawHeaders, "X-Frame-Options",
                    "Mitigates clickjacking attacks by controlling whether page can be framed.",
                    "Set to 'DENY' or 'SAMEORIGIN'."),

                EvaluateHeader(rawHeaders, "Referrer-Policy",
                    "Controls information sent in Referer header with outbound requests.",
                    "Recommend 'strict-origin-when-cross-origin' or 'no-referrer'."),

                EvaluateHeader(rawHeaders, "Permissions-Policy",
                    "Delegates browser features and hardware APIs (camera, geolocation, microphone).",
                    "Specify allowed origins or disable unused device capabilities."),

                EvaluateHeader(rawHeaders, "Cross-Origin-Opener-Policy",
                    "Isolates browsing context to protect against cross-origin attacks (Spectre/COOP).",
                    "Set to 'same-origin' if application requires isolation."),

                EvaluateHeader(rawHeaders, "Cross-Origin-Resource-Policy",
                    "Restricts which origins can read this resource.",
                    "Set to 'same-origin' or 'same-site'."),

                EvaluateHeader(rawHeaders, "Cache-Control",
                    "Specifies caching behavior for sensitive data.",
                    "For sensitive content, use 'no-store, no-cache, must-revalidate'."),

                EvaluateServerHeader(rawHeaders)
            };

            return new HttpSecurityHeadersResult
            {
                Success = true,
                TargetUrl = targetUrl,
                StatusCode = (int)response.StatusCode,
                StatusText = response.ReasonPhrase ?? response.StatusCode.ToString(),
                ResponseTimeMs = sw.ElapsedMilliseconds,
                HeaderChecks = checks,
                RawHeaders = rawHeaders
            };
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return new HttpSecurityHeadersResult
            {
                Success = false,
                TargetUrl = targetUrl,
                ErrorMessage = $"Request timed out after {safeTimeout} seconds."
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "HTTP security header inspection failed for {Url}", targetUrl);
            return new HttpSecurityHeadersResult
            {
                Success = false,
                TargetUrl = targetUrl,
                ErrorMessage = ex.Message
            };
        }
    }

    private static HeaderCheckItem EvaluateHeader(
        Dictionary<string, string> headers,
        string headerName,
        string description,
        string recommendation)
    {
        bool present = headers.TryGetValue(headerName, out var val);

        return new HeaderCheckItem
        {
            HeaderName = headerName,
            IsPresent = present,
            Value = val ?? string.Empty,
            Assessment = present ? "Present" : "Header not present",
            Recommendation = present ? description : recommendation
        };
    }

    private static HeaderCheckItem EvaluateServerHeader(Dictionary<string, string> headers)
    {
        bool present = headers.TryGetValue("Server", out var val);
        bool hasDetailedVersion = present && (val!.Contains('/') || global::System.Text.RegularExpressions.Regex.IsMatch(val, @"\d+\.\d+"));

        return new HeaderCheckItem
        {
            HeaderName = "Server",
            IsPresent = present,
            Value = val ?? string.Empty,
            Assessment = present ? (hasDetailedVersion ? "Configuration may warrant review" : "Present") : "Header not present",
            Recommendation = hasDetailedVersion
                ? "Server header reveals detailed software/version info. Consider minimizing disclosure."
                : "Standard server banner."
        };
    }
}
