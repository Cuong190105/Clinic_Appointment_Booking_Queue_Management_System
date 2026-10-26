using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Clinic.MedicalRecords.Service.Services;

public class MedicalRecordsEventConsumer : BackgroundService
{
    private readonly ILogger<MedicalRecordsEventConsumer> _logger;

    public MedicalRecordsEventConsumer(ILogger<MedicalRecordsEventConsumer> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MedicalRecordsEventConsumer background service started.");
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
