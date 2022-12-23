namespace Certera.Integrations.Notification.Notifications
{
    public class CertificateExpirationNotification : INotification
    {
        private readonly string Domain;
        private readonly string Thumbprint;
        private readonly string DateTime;
        private readonly string DaysText;
        private readonly string PublicKey;
        private readonly string ValidFrom;
        private readonly string ValidTo;

        public CertificateExpirationNotification(string domain, string thumbprint, string dateTime, string daysText, string publicKey, string validFrom, string validTo)
        {
            Domain = domain;
            Thumbprint = thumbprint;
            DateTime = dateTime;
            DaysText = daysText;
            PublicKey = publicKey;
            ValidFrom = validFrom;
            ValidTo = validTo;
        }

        public string ToHtml() => string.Format(htmlTemplate, Domain, Thumbprint, DateTime, DaysText, PublicKey, ValidFrom, ValidTo);

        public string ToMarkdown() => string.Format(markdownTemplate, Domain, Thumbprint, DateTime, DaysText, PublicKey, ValidFrom, ValidTo);

        public string ToPlainText() => string.Format(plainTextTemplate, Domain, Thumbprint, DateTime, DaysText, PublicKey, ValidFrom, ValidTo);

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
                    <pre><b>{0}</b> certificate is set to expire on {2} ({3}).

                        <u>Current details</u>

                        <b>Thumbprint</b>
                        {1}

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

                **{0}** certificate is set to expire on {2} ({3}).

                Current details

                **Thumbprint**
                `{1}`

                **Public Key (hash)**
                `{4}`

                **Valid**
                FROM: `{5}`
                TO: `{6}`

                """;

        private readonly string plainTextTemplate = """

                {0} certificate is set to expire on {2} ({3}).

                Current details
                Thumbprint
                {1}

                Public Key (hash)
                {4}

                Valid
                FROM: {5}
                TO: {6}

                """;
    }
}
