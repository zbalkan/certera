using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Certera.Web.Services.HostedServices
{
    public class QueuedHostedService : BackgroundService
    {
        private readonly ILogger<QueuedHostedService> _logger;

        public IBackgroundTaskQueue TaskQueue { get; }

        public QueuedHostedService(IBackgroundTaskQueue taskQueue,
            ILogger<QueuedHostedService> logger)
        {
            TaskQueue = taskQueue;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Queued Hosted Service is starting.");

            while (!cancellationToken.IsCancellationRequested)
            {
                Func<CancellationToken, Task> workItem;
                try
                {
                    workItem = await TaskQueue.DequeueAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    continue;
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Unknown error getting work item.");
                    continue;
                }

                try
                {
                    await workItem(cancellationToken);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Error occurred executing {nameof(workItem)}.");
                }
            }

            _logger.LogInformation("Queued Hosted Service is stopping.");
        }
    }
}