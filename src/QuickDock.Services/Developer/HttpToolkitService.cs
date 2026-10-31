using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;

namespace QuickDock.Services.Developer;

public sealed class HttpToolkitService : IHttpToolkitService
{
    public const int MaxHttpResponseBodyBytes = 5 * 1024 * 1024; // 5 MB limit
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpToolkitService> _logger;
    private static readonly JsonSerializerOptions PrettyJsonOptions = new() { WriteIndented = true };

    public HttpToolkitService(HttpClient httpClient, ILogger<HttpToolkitService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<HttpResponseModel> SendRequestAsync(HttpRequestModel request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
        {
            return new HttpResponseModel
            {
                StatusCode = 0,
                StatusText = "Invalid URL",
                ErrorMessage = "URL cannot be empty."
            };
        }

        string targetUrl = request.Url.Trim();
        if (targetUrl.Contains("://", StringComparison.Ordinal) || targetUrl.Contains(':'))
        {
            if (Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri) &&
                uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                return new HttpResponseModel
                {
                    StatusCode = 0,
                    StatusText = "Invalid Scheme",
                    ErrorMessage = "Only http and https protocols are supported."
                };
            }
        }

        if (!targetUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !targetUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            targetUrl = "https://" + targetUrl;
        }

        if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out var parsedUri) ||
            (parsedUri.Scheme != Uri.UriSchemeHttp && parsedUri.Scheme != Uri.UriSchemeHttps))
        {
            return new HttpResponseModel
            {
                StatusCode = 0,
                StatusText = "Invalid Scheme",
                ErrorMessage = "Only http and https protocols are supported."
            };
        }

        var method = new HttpMethod(request.Method.ToUpperInvariant());
        using var httpRequest = new HttpRequestMessage(method, parsedUri);

        // Add headers (excluding content headers)
        foreach (var (key, value) in request.Headers)
        {
            if (string.IsNullOrWhiteSpace(key)) continue;

            if (key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                continue;

            httpRequest.Headers.TryAddWithoutValidation(key, value);
        }

        // Add body if applicable
        if (method != HttpMethod.Get && method != HttpMethod.Head && !string.IsNullOrEmpty(request.Body))
        {
            string mediaType = string.IsNullOrWhiteSpace(request.ContentType) ? "application/json" : request.ContentType;
            httpRequest.Content = new StringContent(request.Body, Encoding.UTF8, mediaType);
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        int timeout = Math.Clamp(request.TimeoutSeconds, 1, 300);
        cts.CancelAfter(TimeSpan.FromSeconds(timeout));

        var sw = Stopwatch.StartNew();
        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            sw.Stop();

            // Read response body safely with size bounding to avoid memory exhaustion
            using var stream = await response.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
            using var ms = new MemoryStream();
            byte[] buffer = new byte[8192];
            int read;
            bool truncated = false;
            long totalRead = 0;

            while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cts.Token).ConfigureAwait(false)) > 0)
            {
                if (totalRead + read > MaxHttpResponseBodyBytes)
                {
                    int allowed = MaxHttpResponseBodyBytes - (int)totalRead;
                    if (allowed > 0)
                    {
                        ms.Write(buffer, 0, allowed);
                        totalRead += allowed;
                    }
                    truncated = true;
                    break;
                }
                ms.Write(buffer, 0, read);
                totalRead += read;
            }

            string body = Encoding.UTF8.GetString(ms.ToArray());
            if (truncated)
            {
                body += $"\r\n\r\n[Response body truncated at {MaxHttpResponseBodyBytes / (1024 * 1024)} MB limit by QuickDock Security Policy]";
            }

            long contentLength = response.Content.Headers.ContentLength ?? totalRead;

            var responseHeaders = new Dictionary<string, string>();
            foreach (var h in response.Headers)
            {
                responseHeaders[h.Key] = string.Join(", ", h.Value);
            }
            foreach (var h in response.Content.Headers)
            {
                responseHeaders[h.Key] = string.Join(", ", h.Value);
            }

            string formattedBody = body;
            if (response.Content.Headers.ContentType?.MediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true ||
                body.TrimStart().StartsWith('{') || body.TrimStart().StartsWith('['))
            {
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    formattedBody = JsonSerializer.Serialize(doc, PrettyJsonOptions);
                }
                catch
                {
                    // keep raw body
                }
            }

            return new HttpResponseModel
            {
                StatusCode = (int)response.StatusCode,
                StatusText = response.ReasonPhrase ?? response.StatusCode.ToString(),
                Headers = responseHeaders,
                Body = body,
                FormattedBody = formattedBody,
                ElapsedMilliseconds = sw.ElapsedMilliseconds,
                ContentLengthBytes = contentLength
            };
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return new HttpResponseModel
            {
                StatusCode = 0,
                StatusText = "Request Timeout",
                ElapsedMilliseconds = sw.ElapsedMilliseconds,
                ErrorMessage = $"Request timed out or was canceled after {sw.Elapsed.TotalSeconds:0.##}s."
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "HTTP request failed for {Method} {Url}", request.Method, targetUrl);
            return new HttpResponseModel
            {
                StatusCode = 0,
                StatusText = "Connection Failed",
                ElapsedMilliseconds = sw.ElapsedMilliseconds,
                ErrorMessage = ex.Message
            };
        }
    }
}

