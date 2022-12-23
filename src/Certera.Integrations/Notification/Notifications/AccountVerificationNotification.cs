using System.Text.Encodings.Web;

namespace Certera.Integrations.Notification.Notifications
{
    public class AccountVerificationNotification : INotification
    {
        private readonly string body;

        public AccountVerificationNotification(string callbackUrl)
        {
            var encoded = HtmlEncoder.Default.Encode(callbackUrl);
            body = $"Please confirm your account by <a href='{encoded}'>clicking here</a>.";
        }

        public string ToHtml() => body;
        public string ToMarkdown() => body;
        public string ToPlainText() => body;
    }
}
