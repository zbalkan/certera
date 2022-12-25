namespace Certera.Integrations.Notification.Notifications
{
    public class TestNotification : INotification
    {
        private const string body = "Test email from Certera";

        public string ToHtml() => body;
        public string ToMarkdown() => body;
        public string ToPlainText() => body;
    }
}
