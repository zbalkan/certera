using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Certera.Core.Extensions;
using Certera.Core.Notifications;
using Certera.Data.Models;
using Certera.Integrations.Notification;
using Certera.Integrations.Notification.Notifications;
using Certera.Integrations.Notification.Notifiers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Certera.Web.Services
{
    public class NotificationService : IDisposable
    {
        private readonly MailSender _mailSender;
        private readonly ILogger<NotificationService> _logger;
        private readonly IOptionsSnapshot<MailSenderInfo> _senderInfo;

        public NotificationService(MailSender mailSender, ILogger<NotificationService> logger, IOptionsSnapshot<MailSenderInfo> senderInfo)
        {
            _mailSender = mailSender;
            _logger = logger;
            _senderInfo = senderInfo;

            NotificationDispatcher.Init(logger: logger);
        }

        public async Task SendDomainCertChangeNotificationAsync(IList<NotificationSetting> notificationSettings, IList<DomainCertificateChangeEvent> events)
        {
            foreach (var evt in events)
            {
                foreach (var notification in notificationSettings)
                {
                    var notif = new CertificateChangeNotification(domain: evt.Domain.HostAndPort(),
                                                                               newThumbprint: evt.NewDomainCertificate.Thumbprint,
                                                                               newPublicKey: evt.NewDomainCertificate.Certificate.PublicKeyPinningHash(),
                                                                               newValidFrom: evt.NewDomainCertificate.ValidNotBefore.ToShortDateString(),
                                                                               previousThumbprint: evt.NewDomainCertificate.ValidNotAfter.ToShortDateString(),
                                                                               newValidTo: evt.PreviousDomainCertificate.Thumbprint,
                                                                               previousPublicKey: evt.PreviousDomainCertificate.Certificate.PublicKeyPinningHash(),
                                                                               previousValidFrom: evt.PreviousDomainCertificate.ValidNotBefore.ToShortDateString(),
                                                                               previousValidTo: evt.PreviousDomainCertificate.ValidNotAfter.ToShortDateString());

                    if (notification.SendEmailNotification)
                    {
                        try
                        {
                            _logger.LogInformation($"Sending change notification email for {evt.Domain.HostAndPort()}");

                            var recipients = ExtractRecipients(notification);

                            await NotificationDispatcher.SendNotificationAsync<MailNotifier>(notification: notif,
                                                                    recipients: recipients,
                                                                    subject: $"[certera] {evt.Domain.HostAndPort()} - certificate change notification")
                                .ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error sending certificate change notification email");
                        }
                    }

                    if (notification.SendSlackNotification)
                    {
                        try
                        {
                            _logger.LogInformation($"Sending change notification slack for {evt.Domain.HostAndPort()}");

                            await NotificationDispatcher.SendNotificationAsync<SlackNotifier>(notification: notif).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error sending certificate change notification email");
                        }
                    }
                }

                // Mark the event as processed so it doesn't show up in the next query
                evt.DateProcessed = DateTime.UtcNow;
            }
        }

        public async void SendCertAcquitionFailureNotification(IList<NotificationSetting> notificationSettings,
            AcmeOrder acmeOrder, AcmeOrder lastValidAcmeOrder)
        {
            foreach (var notification in notificationSettings)
            {
                var recipients = ExtractRecipients(notification);

                var lastAcquiryText = "Never";
                var thumbprint = string.Empty;
                var publicKey = string.Empty;
                var validFrom = string.Empty;
                var validTo = string.Empty;

                if (lastValidAcmeOrder?.DomainCertificate != null)
                {
                    lastAcquiryText = lastValidAcmeOrder.DateCreated.ToString();
                    thumbprint = lastValidAcmeOrder.DomainCertificate.Thumbprint;
                    publicKey = lastValidAcmeOrder.DomainCertificate.Certificate.PublicKeyPinningHash();
                    validFrom = lastValidAcmeOrder.DomainCertificate.ValidNotBefore.ToShortDateString();
                    validTo = lastValidAcmeOrder.DomainCertificate.ValidNotAfter.ToShortDateString();
                }

                var notif = new CertificateAcquisitionFailureNotification(domain: acmeOrder.AcmeCertificate.Subject,
                                                                          error: acmeOrder.Errors,
                                                                          lastAcquiryText: lastAcquiryText,
                                                                          thumbprint: thumbprint,
                                                                          publicKey: publicKey,
                                                                          validFrom: validFrom,
                                                                          validTo: validTo);

                if (notification.SendEmailNotification)
                {
                    try
                    {
                        _logger.LogInformation($"Sending certificate acquisition failure notification email for {acmeOrder.AcmeCertificate.Name}");

                        await NotificationDispatcher.SendNotificationAsync<MailNotifier>(notification: notif,
                                                                                         recipients: recipients,
                                                                                         subject: $"[certera] {acmeOrder.AcmeCertificate.Name} - certificate acquisition failure notification");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error sending certificate acquisition failure notification email");
                    }
                }

                if (notification.SendSlackNotification)
                {
                    try
                    {
                        _logger.LogInformation($"Sending acquisition failure notification slack for {acmeOrder.AcmeCertificate.Name}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error sending certificate acquisition failure notification slack");
                    }
                }
            }
        }

        public void SendExpirationNotification(NotificationSetting notificationSetting, Data.Views.TrackedCertificate expiringCert)
        {
            var days = (int)Math.Floor(expiringCert.ValidTo.Value.Subtract(DateTime.Now).TotalDays);
            var daysText = $"{days} {(days == 1 ? " day" : "days")}";

            var canSendEmail = InitEmail(new List<NotificationSetting> { notificationSetting });
            if (canSendEmail && notificationSetting.SendEmailNotification)
            {
                try
                {
                    _logger.LogInformation($"Sending certificate expiration notification email for {expiringCert.Subject}");

                            var recipients = ExtractRecipients(notificationSetting);

                    _mailSender.Send($"[certera] {expiringCert.Subject} - certificate expiration notification",
                        TemplateManager.BuildTemplate(TemplateManager.NotificationCertificateExpirationEmail,
                        new {
                            Domain = expiringCert.Subject,
                            Thumbprint = expiringCert.Thumbprint,
                            DateTime = expiringCert.ValidTo.ToString(),
                            DaysText = daysText,
                            PublicKey = expiringCert.PublicKeyHash,
                            ValidFrom = expiringCert.ValidFrom.Value.ToShortDateString(),
                            ValidTo = expiringCert.ValidTo.Value.ToShortDateString()
                        }),
                        recipients.ToArray());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending certificate expiration notification email");
                }
            }

            if (notificationSetting.SendSlackNotification)
            {
                try
                {
                    _logger.LogInformation($"Sending certificate expiration notification slack for {expiringCert.Subject}");

                    var json = TemplateManager.BuildTemplate(TemplateManager.NotificationCertificateExpirationSlack,
                        new {
                            Domain = expiringCert.Subject,
                            Thumbprint = expiringCert.Thumbprint,
                            DateTime = expiringCert.ValidTo.ToString(),
                            DaysText = daysText,
                            PublicKey = expiringCert.PublicKeyHash,
                            ValidFrom = expiringCert.ValidFrom.Value.ToShortDateString(),
                            ValidTo = expiringCert.ValidTo.Value.ToShortDateString()
                        });

                    SendSlack(notificationSetting.SlackWebhookUrl, json);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending certificate expiration notification slack");
                }
            }
        }

        private void SendSlack(string slackUrl, string json)
        {
        }

        // TODO: Move validation to email sender.
        private bool InitEmail(IList<NotificationSetting> notificationSettings)
        {
            var canSendEmail = !string.IsNullOrWhiteSpace(_senderInfo?.Value?.Host);
            var sendingEmail = notificationSettings.Any(x => x.SendEmailNotification);

            if (sendingEmail && !canSendEmail)
            {
                _logger.LogWarning("SMTP not configured. Unable to send certificate change notifications via email.");
            }
            else if (sendingEmail && canSendEmail)
            {
                _mailSender.Initialize(_senderInfo.Value);
            }

            return canSendEmail;
        }

        private static List<string> ExtractRecipients(NotificationSetting notification)
        {
            var recipients = new List<string>(1)
                            {
                                notification.ApplicationUser.Email
                            };
            if (!string.IsNullOrWhiteSpace(notification.AdditionalRecipients))
            {
                recipients.AddRange(notification.AdditionalRecipients
                    .Split(',', ';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim()));
            }

            return recipients;
        }

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _mailSender?.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion IDisposable Support
    }
}