using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Ocsp;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Ocsp;
using Org.BouncyCastle.X509;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;
using X509Extension = Org.BouncyCastle.Asn1.X509.X509Extension;

namespace Certera.Core.Helpers
{
    public enum OcspStatus
    {
        Good = 0,
        Revoked = 1,
        Unknown = 2,
        ClientError = 3,
        ServerError = 4
    };

    public class OcspClient
    {
        public OcspStatus GetOcspStatus(X509Certificate2 certificate)
        {
            var issuer = GetIssuerCertificate(certificate);

            return GetOcspStatusAsync(certificate, issuer).Result;
        }

        private Task<OcspStatus> GetOcspStatusAsync(X509Certificate2 cert, X509Certificate2 cacert) => GetOcspStatusAsync(ConvertToBCX509Certificate(cert), ConvertToBCX509Certificate(cacert));

        private async Task<OcspStatus> GetOcspStatusAsync(X509Certificate cert, X509Certificate cacert)
        {
            var urls = GetAuthorityInformationAccessOcspUrl(cert);
            if (urls == null || urls.Count == 0)
            {
                throw new Exception("No OCSP URL found in certificate.");
            }

            var url = urls[0];
            Debug.WriteLine("Sending to :  '" + url + "'...");

            var packtosend = CreateOcspPackage(cert, cacert);

            var response = await PostRequestAsync(url, packtosend, "Content-Type", "application/ocsp-request");

            return VerifyResponse(response);
        }

        private byte[] ToByteArray(Stream stream)
        {
            var buffer = new byte[4096 * 8];
            using var ms = new MemoryStream();
            var read = 0;

            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                ms.Write(buffer, 0, read);
            }

            return ms.ToArray();
        }

        private async Task<byte[]> PostRequestAsync(string url, byte[] data, string contentType, string accept)
        {
            var httpClient = new HttpClient();
            var httpRequest = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(url)
            };

            //Set the headers of the request
            httpRequest.Headers.Add("Content-Type", contentType);
            httpRequest.Headers.Add("Content-Length", data.Length.ToString());
            httpRequest.Headers.Add("Accept", accept);

            //A memory stream which is a temporary buffer that holds the payload of the request
            using (var memoryStream = new MemoryStream())
            {
                //Write to the memory stream
                memoryStream.Write(data, 0, data.Length);

                //A stream content that represent the actual request stream
                using (var stream = new StreamContent(memoryStream))
                {
                    httpRequest.Content = stream;

                    //Send the request
                    var response = await httpClient.SendAsync(httpRequest);

                    //you can access the response like that
                    //response.Content

                    Debug.WriteLine(string.Format("HttpStatusCode : {0}", response.StatusCode.ToString()));
                    return ToByteArray(await response.Content.ReadAsStreamAsync());
                }
            }
        }

        private List<string>? GetAuthorityInformationAccessOcspUrl(X509Certificate cert)
        {
            var ocspUrls = new List<string>(1);

            try
            {
                var obj = GetExtensionValue(cert, X509Extensions.AuthorityInfoAccess.Id);

                if (obj == null)
                {
                    return null;
                }
                var s = (Asn1Sequence)obj;
                var elements = s.GetEnumerator();

                while (elements.MoveNext())
                {
                    var element = (Asn1Sequence)elements.Current;
                    var oid = (DerObjectIdentifier)element[0];

                    if (oid.Id.Equals("1.3.6.1.5.5.7.48.1")) // Is Ocsp?
                    {
                        var taggedObject = (Asn1TaggedObject)element[1];
                        var gn = GeneralName.GetInstance(taggedObject);
                        ocspUrls.Add(DerIA5String.GetInstance(gn.Name).GetString());
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception("Error parsing AIA.", e);
            }

            return ocspUrls;
        }

        private OcspStatus VerifyResponse(byte[] response)
        {
            var r = new OcspResp(response);
            var cStatusEnum = OcspStatus.Unknown;
            switch (r.Status)
            {
                case OcspRespStatus.Successful:
                    var or = (BasicOcspResp)r.GetResponseObject();

                    Debug.WriteLine(or.Responses.Length);

                    if (or.Responses.Length == 1)
                    {
                        var resp = or.Responses[0];

                        var certificateStatus = resp.GetCertStatus();

                        if (certificateStatus == null || certificateStatus == CertificateStatus.Good)
                        {
                            cStatusEnum = OcspStatus.Good;
                        }
                        else if (certificateStatus is RevokedStatus)
                        {
                            cStatusEnum = OcspStatus.Revoked;
                        }
                        else if (certificateStatus is UnknownStatus)
                        {
                            cStatusEnum = OcspStatus.Unknown;
                        }
                    }
                    break;

                case OcspResponseStatus.InternalError:
                case OcspResponseStatus.TryLater:
                    cStatusEnum = OcspStatus.ServerError;
                    break;

                case OcspResponseStatus.MalformedRequest:
                case OcspResponseStatus.SignatureRequired:
                case OcspResponseStatus.Unauthorized:
                    cStatusEnum = OcspStatus.ClientError;
                    break;

                default:
                    Debug.WriteLine($"Unknow status '{r.Status}'.");
                    cStatusEnum = OcspStatus.Unknown;
                    break;
            }

            return cStatusEnum;
        }

        private static byte[]? CreateOcspPackage(X509Certificate cert, X509Certificate cacert)
        {
            var gen = new OcspReqGenerator();
            try
            {
                var certId = new CertificateID(CertificateID.HashSha1, cacert, cert.SerialNumber);

                gen.AddRequest(certId);
                gen.SetRequestExtensions(CreateExtension());
                var req = gen.Generate();

                return req.GetEncoded();
            }
            catch (OcspException e)
            {
                Debug.WriteLine(e.StackTrace);
            }
            catch (IOException e)
            {
                Debug.WriteLine(e.StackTrace);
            }

            return null;
        }

        private static X509Extensions CreateExtension()
        {
            var nc = BigInteger.ValueOf(DateTime.Now.Ticks);
            var nonceext = new X509Extension(false, new DerOctetString(nc.ToByteArray()));
            var exts = new Dictionary<DerObjectIdentifier, X509Extension>(1)
            {
                { OcspObjectIdentifiers.PkixOcspNonce, nonceext }
            };
            return new X509Extensions(exts);
        }

        public X509Certificate2 GetIssuerCertificate(X509Certificate2 cert)
        {
            // Self Signed Certificate
            if (cert.Subject == cert.Issuer)
            {
                return cert;
            }

            var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.Build(cert);
            X509Certificate2? issuer = null;

            if (chain.ChainElements.Count > 1)
            {
                issuer = chain.ChainElements[1].Certificate;
            }
            chain.Reset();

            return issuer;
        }

        private static Asn1Object? GetExtensionValue(X509Certificate cert, string oid)
        {
            if (cert == null)
            {
                return null;
            }

            var bytes = cert.GetExtensionValue(new DerObjectIdentifier(oid)).GetOctets();

            if (bytes == null)
            {
                return null;
            }

            var aIn = new Asn1InputStream(bytes);

            return aIn.ReadObject();
        }

        private static X509Certificate ConvertToBCX509Certificate(X509Certificate2 cert)
        {
            var parser = new X509CertificateParser();
            var certarr = cert.Export(X509ContentType.Cert);

            return parser.ReadCertificate(certarr);
        }
    }
}