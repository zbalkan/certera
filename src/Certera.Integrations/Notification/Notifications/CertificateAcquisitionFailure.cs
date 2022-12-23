using System;

namespace Certera.Integrations.Notification.Notifications
{
    public class CertificateAcquisitionFailureNotification : INotification
    {
        private readonly string Domain;
        private readonly string Error;
        private readonly string Thumbprint;
        private readonly string PublicKey;
        private readonly string ValidFrom;
        private readonly string ValidTo;
        private readonly string LastAcquiryText;

        public CertificateAcquisitionFailureNotification(string domain, string error, string lastAcquiryText, string thumbprint, string publicKey, string validFrom, string validTo)
        {
            Domain = domain;
            Error = error;
            LastAcquiryText = lastAcquiryText;
            Thumbprint = thumbprint;
            PublicKey = publicKey;
            ValidFrom = validFrom;
            ValidTo = validTo;
        }

        public string ToHtml() => string.Format(htmlTemplate, Domain, Error, LastAcquiryText, Thumbprint, PublicKey, ValidFrom, ValidTo);

        public string ToMarkdown() => string.Format(markdownTemplate, Domain, Error, LastAcquiryText, Thumbprint, PublicKey, ValidFrom, ValidTo);

        public string ToPlainText() => string.Format(plainTextTemplate, Domain, Error, LastAcquiryText, Thumbprint, PublicKey, ValidFrom, ValidTo);

        private readonly string htmlTemplate = """

                <!doctype html>
                <html>
                <head>
                    <style>
                        body {{
                            font-family: monospace;
                        }}
                    </style>
                </head>
                <body>
                    <pre>There was an error attempting to acquire a certificate for <b>{0}</b>.

                        <b>Error</b>
                        {1}

                        <b>Last acquired</b>
                        {2}

                        <b>Current certificate details</b>
                        <b>Thumbprint</b>
                        {3}

                        <b>Public Key (hash)</b>
                        {4}

                        <b>Valid</b>
                        FROM: {5}
                        TO: {6}
                    </pre>
                </body>
                </html>
                """;

        private readonly string markdownTemplate = """

                There was an error attempting to acquire a certificate for **{0}**.

                **Error**
                `{1}`

                **Last acquired**
                `{2}`

                **Current certificate details**
                **Thumbprint**
                `{3}`

                **Public Key (hash)**
                `{4}`

                **Valid**
                FROM: `{5}`
                TO: `{6}`

                """;

        private readonly string plainTextTemplate = """

                There was an error attempting to acquire a certificate for {0}.

                Error
                {1}

                Last acquired
                {2}

                Current certificate details
                Thumbprint
                {3}

                Public Key (hash)
                {4}

                Valid
                FROM: {5}
                TO: {6}

                """;
    }
}
