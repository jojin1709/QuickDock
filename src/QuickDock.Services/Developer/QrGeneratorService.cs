using System;
using QRCoder;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;

namespace QuickDock.Services.Developer;

public sealed class QrGeneratorService : IQrGeneratorService
{
    public QrGenerationResult GenerateQr(string content, int pixelsPerModule = 10)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new QrGenerationResult
            {
                Success = false,
                ErrorMessage = "QR content cannot be empty."
            };
        }

        try
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
            var qrCode = new PngByteQRCode(data);
            byte[] pngBytes = qrCode.GetGraphic(pixelsPerModule);

            return new QrGenerationResult
            {
                Success = true,
                PngBytes = pngBytes,
                RawPayload = content
            };
        }
        catch (Exception ex)
        {
            return new QrGenerationResult
            {
                Success = false,
                ErrorMessage = $"Failed to generate QR code: {ex.Message}"
            };
        }
    }

    public QrGenerationResult GenerateWiFiQr(string ssid, string password, string authType)
    {
        if (string.IsNullOrWhiteSpace(ssid))
        {
            return new QrGenerationResult
            {
                Success = false,
                ErrorMessage = "Wi-Fi SSID cannot be empty."
            };
        }

        string type = authType?.ToUpperInvariant() switch
        {
            "WEP" => "WEP",
            "NOPASS" or "NONE" => "nopass",
            _ => "WPA"
        };

        // Standard Wi-Fi QR payload: WIFI:S:MySSID;T:WPA;P:MyPassword;;
        string payload = $"WIFI:S:{EscapeWiFiString(ssid)};T:{type};P:{EscapeWiFiString(password ?? "")};;";
        return GenerateQr(payload);
    }

    private static string EscapeWiFiString(string s)
    {
        return s.Replace("\\", "\\\\")
                .Replace(";", "\\;")
                .Replace(",", "\\,")
                .Replace(":", "\\:")
                .Replace("\"", "\\\"");
    }
}
