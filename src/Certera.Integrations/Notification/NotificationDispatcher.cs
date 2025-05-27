using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Certera.Integrations.Notification.Notifications;
using Certera.Integrations.Notification.Notifiers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

namespace Certera.Integrations.Notification
{
    public class NotificationDispatcher
    {
        private readonly Dictionary<Type, Func<INotification, string>> _notificationFormats;

        private readonly Dictionary<Type, INotifier> _notifiers;

        private readonly MailNotifier _mailNotifier;

        private readonly TeamsNotifier _teamsNotifier = new TeamsNotifier();

        private readonly SlackNotifier _slackNotifier = new SlackNotifier();

        private readonly SmsNotifier _smsNotifier = new SmsNotifier();

        private readonly ILogger _logger;

        private readonly Policy _retryPolicy;

        public NotificationDispatcher(ILogger<NotificationDispatcher> logger, IOptionsSnapshot<MailNotifierOptions> options)
        {
            _logger = logger;
            _mailNotifier= new MailNotifier(options);

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

        public async Task SendNotificationAsync<T>(INotification notification, List<string> recipients = null, string subject = null) where T : INotifier
        {
            var body = _notificationFormats[typeof(T)](notification);

            await _retryPolicy.Execute(action: async () => await _notifiers[typeof(T)].TrySendAsync(body, recipients, subject));

            _logger.LogInformation("Notification sent.");
        }

        private string UseHtml(INotification notification) => notification.ToHtml();

        private string UseMarkdown(INotification notification) => notification.ToMarkdown();

        private string UsePlaintext(INotification notification) => notification.ToPlainText();
    }
}
