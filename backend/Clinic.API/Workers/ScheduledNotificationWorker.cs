using Clinic.Application.Interfaces;

namespace Clinic.API.Workers
{
    public class ScheduledNotificationWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public ScheduledNotificationWorker(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _scopeFactory.CreateScope();

                var service = scope.ServiceProvider
                    .GetRequiredService<INotificationService>();

                await service.ProcessDueNotificationsAsync(
                    stoppingToken);

                await Task.Delay(
                    TimeSpan.FromMinutes(30),
                    stoppingToken);
            }
        }
    }
}
