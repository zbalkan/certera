using System;
using System.Collections.Generic;
using Certera.Integrations.Notification.Notifications;
using Certera.Integrations.Notification.Notifiers;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Certera.Integrations.Notification
{
    public static class NotificationDispatcher
    {
        private static Dictionary<Type, Func<INotification, string>> _notificationFormats;

        private static Dictionary<Type, INotifier> _notifiers;

        private static readonly MailNotifier _mailNotifier = new MailNotifier();

        private static readonly TeamsNotifier _teamsNotifier = new TeamsNotifier();

        private static readonly SlackNotifier _slackNotifier = new SlackNotifier();

        private static readonly SmsNotifier _smsNotifier = new SmsNotifier();

        private static ILogger _logger;

        public static void Init(ILogger logger)
        {
            _logger = logger;

            // Use Result pattern instead of bool
            _notificationFormats = new Dictionary<Type, Func<INotification, string>>
            {
                { typeof(MailNotifier), UseHtml },
                { typeof(TeamsNotifier), UseHtml },
                { typeof(SlackNotifier), UseMarkdown },
                { typeof(SmsNotifier), UsePlaintext }
            };

            _notifiers = new Dictionary<Type, INotifier>
            {
                { typeof(MailNotifier), _mailNotifier },
                { typeof(TeamsNotifier), _teamsNotifier },
                { typeof(SlackNotifier), _slackNotifier },
                { typeof(SmsNotifier), _smsNotifier }
            };
        }

        public static async Task SendNotificationAsync<T>(INotification notification, List<string> recipients = null, string subject = null) where T: INotifier
        {
            var body = _notificationFormats[typeof(T)](notification);

            var result = await _notifiers[typeof(T)].TrySendAsync(body, recipients, subject);

            if (!result)
            {
                throw new Exception("Failed to send notification: reason");
            }

            _logger.LogInformation("Notification sent.");
        }

        private static string UseHtml(INotification notification) => notification.ToHtml();

        private static string UseMarkdown(INotification notification) => notification.ToMarkdown();

        private static string UsePlaintext(INotification notification) => notification.ToPlainText();
    }
}
