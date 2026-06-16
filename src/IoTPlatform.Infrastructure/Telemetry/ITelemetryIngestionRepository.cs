using IoTPlatform.Domain.Messaging;

namespace IoTPlatform.Infrastructure.Telemetry;

public interface ITelemetryIngestionRepository
{
    Task<TelemetryIngestionResult> IngestAsync(DeviceTelemetryMessage telemetry, string topic, CancellationToken cancellationToken = default);
}
