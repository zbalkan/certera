﻿using System;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Certera.Data;
using Certera.Web.Extensions;
using Certera.Web.Options;
using Certes;
using Certes.Acme;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Certera.Web
{
    public static class KestrelServerOptionsExtensions
    {
        private static X509Certificate2 _localCert;
        private static X509Certificate2 _tempCert;
        private static X509Certificate2 _lastCert;
        private static long _lastCertId;

        public static void ConfigureEndpoints(this KestrelServerOptions options)
        {
            // Used for accessing certera locally
            _localCert = GenerateSelfSignedCertificate("localhost");

            var configuration = options.ApplicationServices.GetRequiredService<IConfiguration>();

            var httpServer = new HttpServer();
            configuration.GetSection("HTTPServer").Bind(httpServer);

            // Configure HTTP on port 80 on any IP address
            options.ListenAnyIP(80);

            // Configure HTTPS on port 443
            options.ListenAnyIP(443,
                listenOptions => {
                    listenOptions.UseHttps(httpsOptions => {
                        httpsOptions.ServerCertificateSelector = (ctx, name) => {
                            // If we're here, it means we've already completed setup and there
                            // should be a cert.

                            // Try to get the cert and fallback to default localhost cert.
                            // TODO: check for closure issues on "options" below
                            return GetHttpsCertificate(ctx, options, name);
                        };
                    });
                });
        }

        private static X509Certificate2? GetHttpsCertificate(ConnectionContext connectionContext, KestrelServerOptions options, string name)
        {
            // Bail early if we're connecting locally
            if (connectionContext.IsLocal())
            {
                return _localCert;
            }

            using (var scope = options.ApplicationServices.CreateScope())
            {
                var logger = scope.ServiceProvider.GetService<ILogger<Program>>();

                var httpServerOptions = scope.ServiceProvider.GetService<IOptionsSnapshot<HttpServer>>();
                var host = httpServerOptions.Value.SiteHostname;

                // If setup hasn't completed yet, serve a cert with the hostname being requested
                if (string.IsNullOrWhiteSpace(host))
                {
                    // This server could be on a VPS or cloud (i.e. not locally accessible), create
                    // and serve a temporary, self-signed cert for this hostname.
                    _tempCert ??= GenerateSelfSignedCertificate(name);
                    logger.LogDebug($"Serve self-signed certificate for {name}");
                    return _tempCert;
                }

                // A certificate is being requested for some other domain. Ignore it.
                if (!string.Equals(name, host))
                {
                    logger.LogWarning($"Cert requested for {name}, which differs from {host}. Will only attempt to locate certificate for {host}.");
                    return null;
                }

                var dataContext = scope.ServiceProvider.GetService<DataContext>();
                var acmeCert = dataContext.GetAcmeCertificate(host);

                // Build the PFX to be used
                var order = acmeCert?.LatestValidAcmeOrder;
                if (order != null)
                {
                    if (_lastCertId == order.AcmeOrderId)
                    {
                        return _lastCert;
                    }
                    if (order.RawDataPem != null)
                    {
                        var certChain = new CertificateChain(order.RawDataPem);
                        var key = KeyFactory.FromPem(acmeCert.Key.RawData);
                        var pfxBuilder = certChain.ToPfx(key);
                        var pfx = pfxBuilder.Build(host, string.Empty);

                        _lastCertId = order.AcmeOrderId;
                        _lastCert = new X509Certificate2(pfx, string.Empty);
                        return _lastCert;
                    }
                }

                logger.LogWarning($"No certificate found for {host}.");
            }

            return null;
        }

        private static X509Certificate2 GenerateSelfSignedCertificate(string name)
        {
            var distinguishedName = new X500DistinguishedName($"CN={name}");

            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DataEncipherment |
                    X509KeyUsageFlags.KeyEncipherment |
                    X509KeyUsageFlags.DigitalSignature, false));

            request.CertificateExtensions.Add(
               new X509EnhancedKeyUsageExtension(
                   new OidCollection
                   {
                            new Oid("1.3.6.1.5.5.7.3.1"), // server auth
                            new Oid("1.3.6.1.5.5.7.3.2")  // client auth
                   }, false));

            // When creating the localhost certificate, add the other names
            if (string.Equals(name, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                var sanBuilder = new SubjectAlternativeNameBuilder();
                sanBuilder.AddIpAddress(IPAddress.Loopback);
                sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);
                sanBuilder.AddDnsName("localhost");
                sanBuilder.AddDnsName(Environment.MachineName);

                request.CertificateExtensions.Add(sanBuilder.Build());
            }

            var certificate = request.CreateSelfSigned(new DateTimeOffset(DateTime.UtcNow.AddDays(-1)),
                new DateTimeOffset(DateTime.UtcNow.AddDays(90)));

            var tempPwd = Guid.NewGuid().ToString();

            return new X509Certificate2(certificate.Export(X509ContentType.Pfx, tempPwd), tempPwd, X509KeyStorageFlags.MachineKeySet);
        }
    }
}