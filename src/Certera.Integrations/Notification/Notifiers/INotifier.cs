using System.Collections.Generic;
using System.Threading.Tasks;

namespace Certera.Integrations.Notification.Notifiers
{
    public interface INotifier
    {

        /// <summary>
        ///     Send notification.
        /// </summary>
        /// <exception cref="NotificationException">Thrown when sending fails.</exception>
        Task TrySendAsync(string body, List<string> recipients, string? subject = null);
    }
}
