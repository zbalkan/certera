using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Certera.Core.Extensions;
using Certera.Data.Models;
using Certera.Data.Views;
using Certera.Integrations.Notification;
using Certera.Integrations.Notification.Notifications;
using Certera.Integrations.Notification.Notifiers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Certera.Web.Services
{
    public class NotificationService : IDisposable
    {
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(ILogger<NotificationService> logger,
            IOptionsSnapshot<MailNotifierOptions> mailOptions)
        {
            _logger = logger;
            NotificationDispatcher.Init(logger: logger);
            NotificationDispatcher.AddMailNotification(info: mailOptions.Value);
        }

        public async Task SendAccountVerificationNotificationAsync(string callbackUrl, List<string> recipients)
        {
            _logger.LogInformation($"Sending verification email to {string.Join(',', recipients)}.");

            await NotificationDispatcher.SendNotificationAsync<MailNotifier>(notification: new AccountVerificationNotification(callbackUrl),
                                                                       recipients: recipients,
                                                                       subject: "[certera] Confirm your recipient")
                .ConfigureAwait(false);
        }

        public async Task SendCertAcquitionFailureNotificationAsync(IList<NotificationSetting> notificationSettings,
                    AcmeOrder acmeOrder, AcmeOrder lastValidAcmeOrder)
        {
            foreach (var notification in notificationSettings)
            {
                ParseLastValidAcmeOrder(lastValidAcmeOrder, out var lastAcquiryText, out var thumbprint, out var publicKey, out var validFrom, out var validTo);

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
                                                                                         recipients: ExtractRecipients(notification),
                                                                                         subject: $"[certera] {acmeOrder.AcmeCertificate.Name} - certificate acquisition failure notification")
                            .ConfigureAwait(false);
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

                        await NotificationDispatcher.SendNotificationAsync<SlackNotifier>(notification: notif).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error sending certificate acquisition failure notification slack");
                    }
                }
            }
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

                            await NotificationDispatcher.SendNotificationAsync<MailNotifier>(notification: notif,
                                                                    recipients: ExtractRecipients(notification),
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

        public async Task SendExpirationNotificationAsync(NotificationSetting notificationSetting, Data.Views.TrackedCertificate expiringCert)
        {
            var daysText = ExtractDays(expiringCert);

            var notif = new CertificateExpirationNotification(domain: expiringCert.Subject,
                                                              thumbprint: expiringCert.Thumbprint,
                                                              dateTime: expiringCert.ValidTo.ToString(),
                                                              daysText: daysText,
                                                              publicKey: expiringCert.PublicKeyHash,
                                                              validFrom: expiringCert.ValidFrom.Value.ToShortDateString(),
                                                              validTo: expiringCert.ValidTo.Value.ToShortDateString());

            if (notificationSetting.SendEmailNotification)
            {
                try
                {
                    _logger.LogInformation($"Sending certificate expiration notification email for {expiringCert.Subject}");

                    await NotificationDispatcher.SendNotificationAsync<MailNotifier>(notification: notif,
                                                                               recipients: ExtractRecipients(notificationSetting),
                                                                               subject: $"[certera] {expiringCert.Subject} - certificate expiration notification")
                        .ConfigureAwait(false);
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

                    await NotificationDispatcher.SendNotificationAsync<SlackNotifier>(notification: notif).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending certificate expiration notification slack");
                }
            }
        }

        public async Task SendPasswordResetNotificationAsync(string callbackUrl, List<string> recipients)
        {
            _logger.LogInformation($"Sending password reset email to {string.Join(',', recipients)}.");

            await NotificationDispatcher.SendNotificationAsync<MailNotifier>(notification: new AccountVerificationNotification(callbackUrl),
                                                                       recipients: recipients,
                                                                       subject: "[certera] Reset Password")
                .ConfigureAwait(false);
        }

        public async Task SendTestNotificationAsync(List<string> recipients)
        {
            _logger.LogInformation($"Sending test email to {string.Join(',',recipients)}.");

            await NotificationDispatcher.SendNotificationAsync<MailNotifier>(notification: new TestNotification(),
                                                                       recipients: recipients,
                                                                       subject: "[certera] Test Email")
                .ConfigureAwait(false);
        }
        private static string ExtractDays(TrackedCertificate expiringCert)
        {
            var days = (int)Math.Floor(expiringCert.ValidTo.Value.Subtract(DateTime.Now).TotalDays);
            var daysText = $"{days} {(days == 1 ? " day" : "days")}";
            return daysText;
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

        private static void ParseLastValidAcmeOrder(AcmeOrder lastValidAcmeOrder, out string lastAcquiryText, out string thumbprint, out string publicKey, out string validFrom, out string validTo)
        {
            lastAcquiryText = "Never";
            thumbprint = string.Empty;
            publicKey = string.Empty;
            validFrom = string.Empty;
            validTo = string.Empty;
            if (lastValidAcmeOrder?.DomainCertificate != null)
            {
                lastAcquiryText = lastValidAcmeOrder.DateCreated.ToString();
                thumbprint = lastValidAcmeOrder.DomainCertificate.Thumbprint;
                publicKey = lastValidAcmeOrder.DomainCertificate.Certificate.PublicKeyPinningHash();
                validFrom = lastValidAcmeOrder.DomainCertificate.ValidNotBefore.ToShortDateString();
                validTo = lastValidAcmeOrder.DomainCertificate.ValidNotAfter.ToShortDateString();
            }
        }

        // TODO: Move validation to email sender.
        //private bool InitEmail(IList<NotificationSetting> notificationSettings)
        //{
        //    var canSendEmail = !string.IsNullOrWhiteSpace(_senderInfo?.Value?.Host);
        //    var sendingEmail = notificationSettings.Any(x => x.SendEmailNotification);

        //    if (sendingEmail && !canSendEmail)
        //    {
        //        _logger.LogWarning("SMTP not configured. Unable to send certificate change notifications via email.");
        //    }
        //    else if (sendingEmail && canSendEmail)
        //    {
        //        _mailSender.Initialize(_senderInfo.Value);
        //    }

        //    return canSendEmail;
        //}
        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    //_mailSender?.Dispose();
                }

                disposedValue = true;
            }
        }
        #endregion IDisposable Support
    }
}