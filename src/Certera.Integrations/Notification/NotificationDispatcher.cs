using System;
using System.Collections.Generic;
using Certera.Integrations.Notification.Notifications;
using Certera.Integrations.Notification.Notifiers;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;

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

        private static Policy _retryPolicy;

        public static void Init(ILogger logger)
        {
            _logger = logger;


            // Retry a specified number of times, using a function to
            // calculate the duration to wait between retries based on
            // the current retry attempt (allows for exponential back-off)
            // In this case will wait for
            //  2 ^ 1 = 2 seconds then
            //  2 ^ 2 = 4 seconds then
            //  2 ^ 3 = 8 seconds
            _retryPolicy = Policy
              .Handle<NotificationException>()
              .WaitAndRetry(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));        

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

        public static async Task SendNotificationAsync<T>(INotification notification, List<string> recipients = null, string subject = null) where T : INotifier
        {
            var body = _notificationFormats[typeof(T)](notification);

            await _retryPolicy.Execute(async () => {
                await _notifiers[typeof(T)].TrySendAsync(body, recipients, subject); // TODO: Use Poly to retry
            });
            _logger.LogInformation("Notification sent.");
        }

        private static string UseHtml(INotification notification) => notification.ToHtml();

        private static string UseMarkdown(INotification notification) => notification.ToMarkdown();

        private static string UsePlaintext(INotification notification) => notification.ToPlainText();
    }
}
