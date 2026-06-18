namespace IoTPlatform.Application.Realtime;

public sealed record DashboardChangedEvent(
    Guid EventId,
    string Kind,
    Guid? DeviceId,
    string? EntityId,
    DateTimeOffset OccurredAtUtc);

public static class DashboardEventKinds
{
    public const string Telemetry = "telemetry";
    public const string Presence = "presence";
    public const string Command = "command";
    public const string Notification = "notification";
    public const string Automation = "automation";
    public const string Device = "device";
    public const string AvailableDevices = "availableDevices";
}
