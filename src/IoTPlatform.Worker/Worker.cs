namespace IoTPlatform.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IoT MQTT Worker started at {StartedAtUtc}", DateTimeOffset.UtcNow);
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }
}
