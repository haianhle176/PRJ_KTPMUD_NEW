namespace IoTPlatform.Infrastructure.Telemetry;

public sealed record TelemetryIngestionResult(string Serial, int InsertedReadingCount, bool WasDuplicate);
