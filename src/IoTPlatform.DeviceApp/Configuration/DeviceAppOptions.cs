namespace IoTPlatform.DeviceApp.Configuration;

public sealed class DeviceAppOptions
{
    public const string SectionName = "DeviceApp";

    public string DataDirectory { get; init; } = string.Empty;
    public int ReconnectMinSeconds { get; init; } = 2;
    public int ReconnectMaxSeconds { get; init; } = 30;
}
