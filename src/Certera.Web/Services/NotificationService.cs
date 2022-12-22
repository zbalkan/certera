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
            var canSendEmail = InitEmail(notificationSettings);

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

                    if (canSendEmail && notification.SendEmailNotification)
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

        public void SendCertAcquitionFailureNotification(IList<NotificationSetting> notificationSettings,
            AcmeOrder acmeOrder, AcmeOrder lastValidAcmeOrder)
        {
            var canSendEmail = InitEmail(notificationSettings);

            foreach (var notification in notificationSettings)
            {
                if (canSendEmail && notification.SendEmailNotification)
                {
                    try
                    {
                        var recipients = ExtractRecipients(notification);

                        var previousCertText = string.Empty;
                        var lastAcquiryText = "Never";

                        if (lastValidAcmeOrder?.DomainCertificate != null)
                        {
                            lastAcquiryText = lastValidAcmeOrder.DateCreated.ToString();

                            var thumbprint = lastValidAcmeOrder.DomainCertificate.Thumbprint;
                            var publicKey = lastValidAcmeOrder.DomainCertificate.Certificate.PublicKeyPinningHash();
                            var validFrom = lastValidAcmeOrder.DomainCertificate.ValidNotBefore.ToShortDateString();
                            var validTo = lastValidAcmeOrder.DomainCertificate.ValidNotAfter.ToShortDateString();

                            var sb = new StringBuilder(100);
                            sb.AppendLine("<u>Current certificate details</u>")
                                .AppendLine()
                                .AppendLine("<b>Thumbprint</b>")
                                .AppendLine(thumbprint)
                                .AppendLine()
                                .AppendLine("<b>Public Key (hash)</b>")
                                .AppendLine(publicKey)
                                .AppendLine()
                                .AppendLine("<b>Valid</b>")
                                .Append(validFrom).Append(" to ").AppendLine(validTo);
                            previousCertText = sb.ToString();
                        }

                        _logger.LogInformation($"Sending certificate acquisition failure notification email for {acmeOrder.AcmeCertificate.Name}");

                        _mailSender.Send($"[certera] {acmeOrder.AcmeCertificate.Name} - certificate acquisition failure notification",
                            TemplateManager.BuildTemplate(TemplateManager.NotificationCertificateAcquisitionFailureEmail,
                            new {
                                Domain = acmeOrder.AcmeCertificate.Subject,
                                Error = acmeOrder.Errors,
                                PreviousCertificateDetails = previousCertText,
                                LastAcquiryText = lastAcquiryText
                            }),
                            recipients.ToArray());
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

                        var previousCertText = string.Empty;
                        var lastAcquiryText = "Never";

                        if (lastValidAcmeOrder?.DomainCertificate != null)
                        {
                            lastAcquiryText = lastValidAcmeOrder.DateCreated.ToString();

                            var thumbprint = lastValidAcmeOrder.DomainCertificate.Thumbprint;
                            var publicKey = lastValidAcmeOrder.DomainCertificate.Certificate.PublicKeyPinningHash();
                            var validFrom = lastValidAcmeOrder.DomainCertificate.ValidNotBefore.ToShortDateString();
                            var validTo = lastValidAcmeOrder.DomainCertificate.ValidNotAfter.ToShortDateString();

                            var sb = new StringBuilder(100);
                            sb.Append("*Current certificate details*\n")
                                .Append("*Thumbprint:*\n").AppendLine(thumbprint)
                                .AppendLine("*Public Key (hash):*").AppendLine(publicKey)
                                .AppendLine("*Valid:*").Append(validFrom).Append(" to ").Append(validTo);
                            previousCertText = sb.ToString();
                        }

                        var json = TemplateManager.BuildTemplate(TemplateManager.NotificationCertificateAcquisitionFailureSlack,
                            new {
                                Domain = acmeOrder.AcmeCertificate.Subject,
                                Error = acmeOrder.Errors,
                                PreviousCertificateDetails = previousCertText,
                                LastAcquiryText = lastAcquiryText
                            });

                        SendSlack(notification.SlackWebhookUrl, json);
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