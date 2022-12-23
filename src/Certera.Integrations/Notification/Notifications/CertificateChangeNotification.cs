namespace Certera.Integrations.Notification.Notifications
{
    public class CertificateChangeNotification : INotification
    {
        private readonly string Domain;
        private readonly string NewThumbprint;
        private readonly string NewPublicKey;
        private readonly string NewValidFrom;
        private readonly string NewValidTo;
        private readonly string PreviousThumbprint;
        private readonly string PreviousPublicKey;
        private readonly string PreviousValidFrom;
        private readonly string PreviousValidTo;

        public CertificateChangeNotification(string domain, string newThumbprint, string newPublicKey, string newValidFrom, string previousThumbprint, string newValidTo, string previousPublicKey, string previousValidFrom, string previousValidTo)
        {
            Domain = domain;
            NewThumbprint = newThumbprint;
            NewPublicKey = newPublicKey;
            NewValidFrom = newValidFrom;
            NewValidTo = newValidTo;
            PreviousThumbprint = previousThumbprint;
            PreviousPublicKey = previousPublicKey;
            PreviousValidFrom = previousValidFrom;
            PreviousValidTo = previousValidTo;
        }

        public string ToHtml() => string.Format(htmlTemplate,
            Domain, NewThumbprint, NewPublicKey, NewValidFrom, NewValidTo, PreviousThumbprint, PreviousPublicKey, PreviousValidFrom, PreviousValidTo);

        public string ToMarkdown() => string.Format(markdownTemplate,
            Domain, NewThumbprint, NewPublicKey, NewValidFrom, NewValidTo, PreviousThumbprint, PreviousPublicKey, PreviousValidFrom, PreviousValidTo);

        public string ToPlainText() => string.Format(plainTextTemplate,
            Domain, NewThumbprint, NewPublicKey, NewValidFrom, NewValidTo, PreviousThumbprint, PreviousPublicKey, PreviousValidFrom, PreviousValidTo);

        private readonly string htmlTemplate = """

                <!doctype html>
                <html>
                <head>
                    <style>
                        body {{font - family: monospace; }}
                    </style>
                </head>
                <body>
                    <pre>Change detected in the <b>{0}</b> certificate.

                <u>New certificate details</u>

                <b>Thumbprint</b>
                {1}

                <b>Public Key (hash)</b>
                {2}

                <b>Valid</b>
                {3} to {4}

                <u>Previous certificate details</u>

                <b>Thumbprint</b>
                {5}

                <b>Public Key (hash)</b>
                {6}

                <b>Valid</b>
                {7} to {8}
                    </pre>
                </body>
                </html>


                """;
        private readonly string markdownTemplate = """

                Change detected in the **{0}** certificate.

                New certificate details

                **Thumbprint**
                `{1}`

                **Public Key (hash)**
                `{2}`

                **Valid**
                `{3} to {4}`

                Previous certificate details

                **Thumbprint**
                `{5}`

                **Public Key (hash)**
                `{6}`

                **Valid**
                `{7} to {8}`
                """;

        private readonly string plainTextTemplate = """

                Change detected in the {0} certificate.

                New certificate details

                Thumbprint
                {1}

                Public Key (hash)
                {2}

                Valid
                {3} to {4}

                Previous certificate details

                Thumbprint
                {5}

                Public Key (hash)
                {6}

                Valid
                {7} to {8}

                """;
    }
}
