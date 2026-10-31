using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;

namespace QuickDock.Services.NetworkSecurity;

public sealed class TlsInspectionService : ITlsInspectionService
{
    private readonly ILogger<TlsInspectionService> _logger;

    public TlsInspectionService(ILogger<TlsInspectionService> logger)
    {
        _logger = logger;
    }

    public async Task<TlsInspectionResult> InspectTlsAsync(
        string host,
        int port = 443,
        int timeoutSeconds = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return new TlsInspectionResult
            {
                Success = false,
                TargetHost = host,
                TargetPort = port,
                ErrorMessage = "Host cannot be empty."
            };
        }

        string cleanHost = host.Trim();
        if (cleanHost.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(cleanHost);
                cleanHost = uri.Host;
                if (!uri.IsDefaultPort && uri.Port > 0) port = uri.Port;
            }
            catch
            {
                cleanHost = cleanHost.Replace("https://", "").Split('/')[0];
            }
        }
        else if (cleanHost.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            cleanHost = cleanHost.Replace("http://", "").Split('/')[0];
        }

        int colon = cleanHost.IndexOf(':');
        if (colon > 0 && int.TryParse(cleanHost[(colon + 1)..], out int customPort))
        {
            port = customPort;
            cleanHost = cleanHost[..colon];
        }

        int safePort = Math.Clamp(port, 1, 65535);
        int safeTimeout = Math.Clamp(timeoutSeconds, 1, 60);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(safeTimeout));

        X509Certificate2? capturedCert = null;
        X509Chain? capturedChain = null;
        SslPolicyErrors capturedErrors = SslPolicyErrors.None;

        try
        {
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(cleanHost, safePort, cts.Token);

            using var sslStream = new SslStream(
                tcpClient.GetStream(),
                leaveInnerStreamOpen: false,
                (sender, certificate, chain, sslPolicyErrors) =>
                {
                    capturedErrors = sslPolicyErrors;
                    if (certificate != null)
                    {
                        capturedCert = new X509Certificate2(certificate);
                    }
                    if (chain != null)
                    {
                        // Clone chain elements
                        capturedChain = new X509Chain();
                        capturedChain.Build(capturedCert!);
                    }
                    // Allow handshake to complete so we can inspect negotiated protocol & cipher
                    return true;
                });

            var sslOptions = new SslClientAuthenticationOptions
            {
                TargetHost = cleanHost,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            };

            await sslStream.AuthenticateAsClientAsync(sslOptions, cts.Token);

            string protocol = sslStream.SslProtocol.ToString();
            string cipherSuite = sslStream.NegotiatedCipherSuite.ToString();

            if (capturedCert == null)
            {
                return new TlsInspectionResult
                {
                    Success = false,
                    TargetHost = cleanHost,
                    TargetPort = safePort,
                    ErrorMessage = "No remote certificate was presented during TLS handshake."
                };
            }

            var sans = new List<string>();
            foreach (var ext in capturedCert.Extensions)
            {
                if (ext is X509SubjectAlternativeNameExtension sanExt)
                {
                    foreach (var dnsName in sanExt.EnumerateDnsNames())
                    {
                        sans.Add(dnsName);
                    }
                    foreach (var ip in sanExt.EnumerateIPAddresses())
                    {
                        sans.Add(ip.ToString());
                    }
                }
            }

            var chainElements = new List<TlsChainElement>();
            if (capturedChain != null)
            {
                foreach (var el in capturedChain.ChainElements)
                {
                    chainElements.Add(new TlsChainElement
                    {
                        Subject = el.Certificate.Subject,
                        Issuer = el.Certificate.Issuer,
                        Thumbprint = el.Certificate.Thumbprint
                    });
                }
            }

            bool isValidationOk = capturedErrors == SslPolicyErrors.None;
            string validationMsg = isValidationOk
                ? "Certificate chain and hostname verified successfully."
                : $"Certificate validation warning: {capturedErrors}";

            return new TlsInspectionResult
            {
                Success = true,
                TargetHost = cleanHost,
                TargetPort = safePort,
                NegotiatedProtocol = protocol,
                CipherSuite = cipherSuite,
                Subject = capturedCert.Subject,
                CommonName = capturedCert.GetNameInfo(X509NameType.SimpleName, false),
                Issuer = capturedCert.Issuer,
                ValidFrom = capturedCert.NotBefore,
                ValidUntil = capturedCert.NotAfter,
                Thumbprint = capturedCert.Thumbprint,
                SerialNumber = capturedCert.SerialNumber,
                SignatureAlgorithm = capturedCert.SignatureAlgorithm.FriendlyName ?? capturedCert.SignatureAlgorithm.Value ?? "Unknown",
                SubjectAlternativeNames = sans,
                CertificateChain = chainElements,
                IsValidationSuccessful = isValidationOk,
                ValidationStatusMessage = validationMsg
            };
        }
        catch (OperationCanceledException)
        {
            return new TlsInspectionResult
            {
                Success = false,
                TargetHost = cleanHost,
                TargetPort = safePort,
                ErrorMessage = $"TLS connection timed out after {safeTimeout} seconds."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TLS inspection failed for {Host}:{Port}", cleanHost, safePort);
            return new TlsInspectionResult
            {
                Success = false,
                TargetHost = cleanHost,
                TargetPort = safePort,
                ErrorMessage = ex.Message
            };
        }
        finally
        {
            capturedCert?.Dispose();
            capturedChain?.Dispose();
        }
    }
}
