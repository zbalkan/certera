using System.Text.Encodings.Web;

namespace Certera.Integrations.Notification.Notifications
{
    public class PasswordResetNotification : INotification
    {
        private readonly string body;

        public PasswordResetNotification(string callbackUrl)
        {
            var encoded = HtmlEncoder.Default.Encode(callbackUrl);
            body = $"Please reset your password by <a href='{encoded}'>clicking here</a>.";
        }

        public string ToHtml() => body;
        public string ToMarkdown() => body;
        public string ToPlainText() => body;
    }
}
