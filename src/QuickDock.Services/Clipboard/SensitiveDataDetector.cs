using System.Text.RegularExpressions;
using QuickDock.Core.Interfaces;

namespace QuickDock.Services.Clipboard;

public sealed partial class SensitiveDataDetector : ISensitiveDataDetector
{
    // Regex patterns for obvious secrets
    private static readonly Regex JwtRegex = new(@"\beyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\b", RegexOptions.Compiled);
    private static readonly Regex AwsKeyRegex = new(@"\b(AKIA|ABIA|ACCA|ASIA)[0-9A-Z]{16}\b", RegexOptions.Compiled);
    private static readonly Regex GitHubTokenRegex = new(@"\b(ghp|gho|ghu|ghs|ghr)_[A-Za-z0-9_]{36,}\b", RegexOptions.Compiled);
    private static readonly Regex OpenAiKeyRegex = new(@"\bsk-[A-Za-z0-9]{20,}\b", RegexOptions.Compiled);
    private static readonly Regex SlackTokenRegex = new(@"\bxox[baprs]-[0-9A-Za-z-]{20,}\b", RegexOptions.Compiled);
    private static readonly Regex PrivateKeyRegex = new(@"-----BEGIN[ A-Z0-9_-]*PRIVATE KEY-----", RegexOptions.Compiled);
    private static readonly Regex GenericApiKeyAssignmentRegex = new(@"(api[_-]?key|access[_-]?token|auth[_-]?token|secret[_-]?key)\s*[:=]\s*['""]?[A-Za-z0-9_\-]{16,}['""]?", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public bool IsSensitive(string text, out string? reason)
    {
        reason = null;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var trimmed = text.Trim();

        if (PrivateKeyRegex.IsMatch(trimmed))
        {
            reason = "Private Key detected";
            return true;
        }

        if (JwtRegex.IsMatch(trimmed))
        {
            reason = "JSON Web Token (JWT) detected";
            return true;
        }

        if (AwsKeyRegex.IsMatch(trimmed))
        {
            reason = "AWS Access Key detected";
            return true;
        }

        if (GitHubTokenRegex.IsMatch(trimmed))
        {
            reason = "GitHub Personal Access Token detected";
            return true;
        }

        if (OpenAiKeyRegex.IsMatch(trimmed))
        {
            reason = "API Secret Key detected";
            return true;
        }

        if (SlackTokenRegex.IsMatch(trimmed))
        {
            reason = "Authentication Token detected";
            return true;
        }

        if (GenericApiKeyAssignmentRegex.IsMatch(trimmed))
        {
            reason = "API Key or Secret pattern detected";
            return true;
        }

        return false;
    }
}

