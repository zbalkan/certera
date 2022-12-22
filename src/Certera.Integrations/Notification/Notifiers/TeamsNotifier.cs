using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Certera.Integrations.Notification.Notifiers
{

    public class TeamsNotifier : INotifier
    {
        public async Task<bool> TrySendAsync(string body, List<string> recipients, string subject = null) => throw new NotImplementedException();
    }
}
