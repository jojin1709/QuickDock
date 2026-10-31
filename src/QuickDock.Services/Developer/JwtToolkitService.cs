using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;

namespace QuickDock.Services.Developer;

public sealed class JwtToolkitService : IJwtToolkitService
{
    private static readonly JsonSerializerOptions PrettyPrintOptions = new() { WriteIndented = true };

    public JwtTokenInfo Decode(string rawToken, string? verificationSecret = null)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return new JwtTokenInfo
            {
                IsDecoded = false,
                VerificationMessage = "Token cannot be empty."
            };
        }

        string trimmed = rawToken.Trim();
        var parts = trimmed.Split('.');
        if (parts.Length != 3)
        {
            return new JwtTokenInfo
            {
                RawToken = trimmed,
                IsDecoded = false,
                VerificationMessage = $"Invalid JWT format: expected 3 parts separated by '.', got {parts.Length}."
            };
        }

        try
        {
            string headerJson = Base64UrlDecode(parts[0]);
            string payloadJson = Base64UrlDecode(parts[1]);
            string signatureHex = Convert.ToHexString(Base64UrlDecodeBytes(parts[2]));

            // Format JSONs
            string prettyHeader = FormatJson(headerJson);
            string prettyPayload = FormatJson(payloadJson);

            // Parse header properties
            string algorithm = "Unknown";
            string type = "JWT";
            using (var headerDoc = JsonDocument.Parse(headerJson))
            {
                if (headerDoc.RootElement.TryGetProperty("alg", out var algProp))
                    algorithm = algProp.GetString() ?? "Unknown";
                if (headerDoc.RootElement.TryGetProperty("typ", out var typProp))
                    type = typProp.GetString() ?? "JWT";
            }

            // Parse payload properties & claims
            string? sub = null;
            string? iss = null;
            string? aud = null;
            DateTimeOffset? exp = null;
            DateTimeOffset? nbf = null;
            DateTimeOffset? iat = null;
            var claims = new Dictionary<string, string>();

            using (var payloadDoc = JsonDocument.Parse(payloadJson))
            {
                foreach (var prop in payloadDoc.RootElement.EnumerateObject())
                {
                    string key = prop.Name;
                    string val = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String => prop.Value.GetString() ?? "",
                        JsonValueKind.Number => prop.Value.GetRawText(),
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        JsonValueKind.Null => "null",
                        _ => prop.Value.GetRawText()
                    };

                    claims[key] = val;

                    switch (key)
                    {
                        case "sub":
                            sub = val;
                            break;
                        case "iss":
                            iss = val;
                            break;
                        case "aud":
                            aud = val;
                            break;
                        case "exp":
                            if (long.TryParse(val, out long expSec))
                                exp = DateTimeOffset.FromUnixTimeSeconds(expSec);
                            break;
                        case "nbf":
                            if (long.TryParse(val, out long nbfSec))
                                nbf = DateTimeOffset.FromUnixTimeSeconds(nbfSec);
                            break;
                        case "iat":
                            if (long.TryParse(val, out long iatSec))
                                iat = DateTimeOffset.FromUnixTimeSeconds(iatSec);
                            break;
                    }
                }
            }

            // Check signature verification if secret is provided
            bool isVerified = false;
            string verificationMsg;

            if (!string.IsNullOrEmpty(verificationSecret))
            {
                if (algorithm.Equals("HS256", StringComparison.OrdinalIgnoreCase))
                {
                    string headerAndPayload = $"{parts[0]}.{parts[1]}";
                    byte[] keyBytes = Encoding.UTF8.GetBytes(verificationSecret);
                    using var hmac = new HMACSHA256(keyBytes);
                    byte[] computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(headerAndPayload));
                    byte[] signatureBytes = Base64UrlDecodeBytes(parts[2]);

                    if (CryptographicOperations.FixedTimeEquals(computedHash, signatureBytes))
                    {
                        isVerified = true;
                        verificationMsg = "Signature VERIFIED successfully with provided secret (HS256).";
                    }
                    else
                    {
                        isVerified = false;
                        verificationMsg = "Signature verification FAILED. Secret does not match signature.";
                    }
                }
                else
                {
                    isVerified = false;
                    verificationMsg = $"Local HMAC verification only supports HS256 (token algorithm is {algorithm}).";
                }
            }
            else
            {
                verificationMsg = "Decoding a JWT does not verify its signature. Enter a secret key to verify.";
            }

            return new JwtTokenInfo
            {
                RawToken = trimmed,
                Algorithm = algorithm,
                Type = type,
                Subject = sub,
                Issuer = iss,
                Audience = aud,
                ExpiresAt = exp,
                NotBefore = nbf,
                IssuedAt = iat,
                Claims = claims,
                HeaderJson = prettyHeader,
                PayloadJson = prettyPayload,
                SignatureHex = signatureHex,
                IsDecoded = true,
                IsVerified = isVerified,
                VerificationMessage = verificationMsg
            };
        }
        catch (Exception ex)
        {
            return new JwtTokenInfo
            {
                RawToken = trimmed,
                IsDecoded = false,
                VerificationMessage = $"Failed to decode token: {ex.Message}"
            };
        }
    }

    private static string FormatJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc, PrettyPrintOptions);
        }
        catch
        {
            return json;
        }
    }

    private static string Base64UrlDecode(string input)
    {
        byte[] bytes = Base64UrlDecodeBytes(input);
        return Encoding.UTF8.GetString(bytes);
    }

    private static byte[] Base64UrlDecodeBytes(string input)
    {
        string padded = input.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
    }
}
