using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using MailKit.Net.Smtp;
using MimeKit;

namespace Certera.Integrations.Notification.Notifiers
{
    public class MailNotifier : INotifier
    {
        private readonly SmtpClient _client;
        private readonly MailNotifierOptions _info;

        public MailNotifier(MailNotifierOptions info)
        {
            _client = new SmtpClient();
            _info = info;
        }


        public async Task TrySendAsync(string body, List<string> recipients, string subject = null)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_info.FromName, _info.FromEmail));
            message.To.AddRange(recipients.Select(x => MailboxAddress.Parse(x)));
            message.Subject = subject;
            message.Body = new TextPart(MimeKit.Text.TextFormat.Html)
            {
                Text = body
            };

            await EnsureConnectedAsync();

            await _client.SendAsync(message);
        }

        private async Task EnsureConnectedAsync()
        {
            if (!_client.IsConnected)
            {
                await _client.ConnectAsync(_info.Host, _info.Port, _info.UseSsl).ConfigureAwait(false);

                if (_info.Username != null || _info.Password != null)
                {
                    await _client.AuthenticateAsync(_info.Username, _info.Password).ConfigureAwait(false);
                }
            }
        }

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _client?.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose() => Dispose(true);

        #endregion IDisposable Support
    }

    public class MailNotifierOptions
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool UseSsl { get; set; }
        public string FromEmail { get; set; }
        public string FromName { get; set; }
    }
}
