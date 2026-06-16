namespace IoTPlatform.DeviceApp.Configuration;

public sealed class DeviceHiveMqOptions
{
    public const string SectionName = "HiveMq";

    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 8883;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}
