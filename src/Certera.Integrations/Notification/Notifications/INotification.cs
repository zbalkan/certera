namespace Certera.Integrations.Notification.Notifications
{
    public interface INotification
    {
        string ToHtml();
        string ToMarkdown();
        string ToPlainText();
    }
}
