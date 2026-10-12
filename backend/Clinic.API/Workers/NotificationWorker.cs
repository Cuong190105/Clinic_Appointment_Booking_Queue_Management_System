using Clinic.Application.Interfaces;
using Clinic.Application.Services;

namespace Clinic.API.Workers
{
    public class NotificationWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public NotificationWorker(
            IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _scopeFactory.CreateScope();

                var queue = scope.ServiceProvider.GetRequiredService<INotificationQueue>();

                var notificationService =
                    scope.ServiceProvider
                        .GetRequiredService<INotificationService>();

                await foreach (var job in queue.ReadAllAsync(stoppingToken))
                {
                    await notificationService.SendAsync(
                        job,
                        stoppingToken);
                }
                await Task.Delay(
                    TimeSpan.FromSeconds(1),
                    stoppingToken);
            }
        }
    }
}
