using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Certera.Core.Extensions
{
    public static class X509Certificate2Extensions
    {
        public static bool ExpiresWithinDays(this X509Certificate2 cert, int days) => DateTime.Now.Date >= cert.NotAfter.Subtract(TimeSpan.FromDays(days)).Date;

        public static string PublicKeyPinningHash(this X509Certificate2 cert)
        {
            // Get the SubjectPublicKeyInfo member of the certificate
            var subjectPublicKeyInfo = GetSubjectPublicKeyInfoRaw(cert);

            // Take the SHA2-256 hash of the DER ASN.1 encoded value
            // And convert hash to base64
            return Convert.ToBase64String(SHA256.HashData(subjectPublicKeyInfo));
        }

        private static byte[] GetSubjectPublicKeyInfoRaw(X509Certificate2 x509Cert)
        {
            var rawCert = x509Cert.GetRawCertData();

            /*
             Certificate is, by definition:

                Certificate  ::=  SEQUENCE  {
                    tbsCertificate       TBSCertificate,
                    signatureAlgorithm   AlgorithmIdentifier,
                    signatureValue       BIT STRING
                }

               TBSCertificate  ::=  SEQUENCE  {
                    version         [0]  EXPLICIT Version DEFAULT v1,
                    serialNumber         CertificateSerialNumber,
                    signature            AlgorithmIdentifier,
                    issuer               Name,
                    validity             Validity,
                    subject              Name,
                    subjectPublicKeyInfo SubjectPublicKeyInfo,
                    issuerUniqueID  [1]  IMPLICIT UniqueIdentifier OPTIONAL, -- If present, version MUST be v2 or v3
                    subjectUniqueID [2]  IMPLICIT UniqueIdentifier OPTIONAL, -- If present, version MUST be v2 or v3
                    extensions      [3]  EXPLICIT Extensions       OPTIONAL  -- If present, version MUST be v3
                }

            So we walk to ASN.1 DER tree in order to drill down to the SubjectPublicKeyInfo item
            */
            var list = AsnNext(ref rawCert, true); //unwrap certificate sequence
            var tbsCertificate = AsnNext(ref list, false); //get next item; which is tbsCertificate
            list = AsnNext(ref tbsCertificate, true); //unwap tbsCertificate sequence

            var version = AsnNext(ref list, false); //tbsCertificate.Version
            var serialNumber = AsnNext(ref list, false); //tbsCertificate.SerialNumber
            var signature = AsnNext(ref list, false); //tbsCertificate.Signature
            var issuer = AsnNext(ref list, false); //tbsCertificate.Issuer
            var validity = AsnNext(ref list, false); //tbsCertificate.Validity
            var subject = AsnNext(ref list, false); //tbsCertificate.Subject
                                                    //tbsCertificate.SubjectPublicKeyInfo
            return AsnNext(ref list, false);
        }

        private static byte[] AsnNext(ref byte[] buffer, bool unwrap)
        {
            //Public Domain: No attribution required
            byte[] result;

            if (buffer.Length < 2)
            {
                result = buffer;
                buffer = Array.Empty<byte>();
                return result;
            }

            var index = 0;
            var entityType = buffer[index];
            index++;

            int length = buffer[index];
            index++;

            var lengthBytes = 1;
            if (length >= 0x80)
            {
                lengthBytes = length & 0x0F; //low nibble is number of length bytes to follow
                length = 0;

                for (var i = 0; i < lengthBytes; i++)
                {
                    length = (length << 8) + (int)buffer[2 + i];
                    index++;
                }
                lengthBytes++;
            }

            int copyStart;
            int copyLength;
            if (unwrap)
            {
                copyStart = 1 + lengthBytes;
                copyLength = length;
            }
            else
            {
                copyStart = 0;
                copyLength = 1 + lengthBytes + length;
            }
            result = new byte[copyLength];
            Array.Copy(buffer, copyStart, result, 0, copyLength);

            var remaining = new byte[buffer.Length - (copyStart + copyLength)];
            if (remaining.Length > 0)
            {
                Array.Copy(buffer, copyStart + copyLength, remaining, 0, remaining.Length);
            }

            buffer = remaining;

            return result;
        }
    }
}