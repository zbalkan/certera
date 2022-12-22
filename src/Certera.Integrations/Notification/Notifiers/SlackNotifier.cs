using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Certera.Integrations.Notification.Notifiers
{
    public class SlackNotifier : INotifier
    {
        private readonly HttpClient _slackClient;

        public SlackNotifier()
        {
            _slackClient = new HttpClient();
        }

        public async Task<bool> TrySendAsync(string body, List<string> recipients, string subject = null)
        {
            var content = new StringContent(body, Encoding.UTF8, "application/json");

            HttpResponseMessage httpResponse;
            try
            {
                httpResponse = await _slackClient.PostAsync("slackUrl", content);
            }
            catch (HttpRequestException ex)
            {
                // TODO: Log exception
                return false;
            }

            if (httpResponse.Content != null)
            {
                var responseContent = await httpResponse.Content.ReadAsStringAsync();

                // TODO: Log response
            }

            return true;
        }
    }
}
