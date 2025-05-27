using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Certera.Data;
using Certera.Web.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Certera.Web.Services.HostedServices
{
    public class CertificateChangeNotificationService : IHostedService, IDisposable
    {
        private readonly IServiceProvider _services;
        private readonly ILogger _logger;
        private Timer _timer;
        private bool _running;

        public CertificateChangeNotificationService(IServiceProvider services,
            ILogger<CertificateChangeNotificationService> logger)
        {
            _services = services;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Certificate change notification service starting.");

            _timer = new Timer(callback: TimerIntervalCallback, state: null,
                dueTime: TimeSpan.FromMinutes(3) /* start */,
                period: TimeSpan.FromMinutes(60) /* interval */);

            return Task.CompletedTask;
        }

        private void TimerIntervalCallback(object state)
        {
            if (_running)
            {
                _logger.LogInformation("Certificate change notification job still running.");
                return;
            }
            _running = true;
            _logger.LogInformation("Certificate change notification job started.");

            RunNotificationCheckAsync().GetAwaiter(); // We need to wait for completion.

            _logger.LogInformation("Certificate change notification job completed.");
            _running = false;
        }

        private async Task RunNotificationCheckAsync()
        {
            using var scope = _services.CreateScope();
            try
            {
                var setupOptions = scope.ServiceProvider.GetService<IOptionsSnapshot<Setup>>();
                if (setupOptions?.Value.Finished == false)
                {
                    _logger.LogInformation("Skipping execution of certificate change notification service because setup is not complete.");
                    return;
                }

                using var notificationService = scope.ServiceProvider.GetService<NotificationService>();
                var dataContext = scope.ServiceProvider.GetService<DataContext>();
                if (dataContext == null)
                {
                    throw new AggregateException("DataContext service is null.");
                }

                // Get the change events that were created when scans occurred
                var events = await dataContext.DomainCertificateChangeEvents
                    .Include(x => x.Domain)
                    .Include(x => x.NewDomainCertificate)
                    .Include(x => x.PreviousDomainCertificate)
                    .Where(x => x.DateProcessed == null)
                    .ToListAsync();

                var notificationSettings = await dataContext.NotificationSettings
                    .Include(x => x.ApplicationUser)
                    .Where(x => x.ChangeAlerts)
                    .ToListAsync();

                await notificationService.SendDomainCertChangeNotificationAsync(notificationSettings, events);

                // User notified, save the record
                dataContext.SaveChanges();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Certificate change notification job error.");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Certificate change notification service stopping.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // Dispose managed state (managed objects).
                    _timer?.Dispose();
                }

                disposedValue = true;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize", Justification = "No unmanaged resources")]
        public void Dispose() => Dispose(true);

        #endregion IDisposable Support
    }
}