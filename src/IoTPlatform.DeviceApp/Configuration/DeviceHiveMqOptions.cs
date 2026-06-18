namespace IoTPlatform.DeviceApp.Configuration;

public sealed class DeviceHiveMqOptions
{
    public const string SectionName = "HiveMq";

    public string Host { get; init; } = "f1f0f565b8564081b04217852589bb65.s1.eu.hivemq.cloud";
    public int Port { get; init; } = 8883;
    public string Username { get; init; } = "Themdauacong123";
    public string Password { get; init; } = "Themdauacong123@";
}
