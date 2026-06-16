namespace IoTPlatform.Infrastructure.Telemetry;

public sealed class TelemetryIngestionException(string message) : InvalidOperationException(message);
