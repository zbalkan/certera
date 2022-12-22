using System.Collections.Generic;
using System.Threading.Tasks;

namespace Certera.Integrations.Notification.Notifiers
{
    public interface INotifier
    {

        // Recipients can be API webhooks, channel names, etc.
        Task<bool> TrySendAsync(string body, List<string> recipients, string? subject = null);
    }
}
