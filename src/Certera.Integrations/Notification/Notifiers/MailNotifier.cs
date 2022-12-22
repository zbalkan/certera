using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Certera.Integrations.Notification.Notifiers
{

    public class MailNotifier : INotifier
    {
        public async Task TrySendAsync(string body, List<string> recipients, string subject = null) => throw new NotImplementedException();
    }
}
