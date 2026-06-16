namespace IoTPlatform.Domain.Messaging;

public static class MqttTopics
{
    public const string DeviceWildcard = "iot/devices/+/+";

    public static string Manifest(string serial) => $"iot/devices/{serial}/manifest";
    public static string Telemetry(string serial) => $"iot/devices/{serial}/telemetry";
    public static string State(string serial) => $"iot/devices/{serial}/state";
    public static string Commands(string serial) => $"iot/devices/{serial}/commands";
    public static string CommandAcknowledgements(string serial) => $"iot/devices/{serial}/commands/ack";
    public static string Presence(string serial) => $"iot/devices/{serial}/presence";

    public static bool TryParse(string topic, out string serial, out string messageType)
    {
        serial = string.Empty;
        messageType = string.Empty;
        var segments = topic.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length < 4 || segments[0] != "iot" || segments[1] != "devices")
        {
            return false;
        }

        serial = segments[2];
        messageType = string.Join('/', segments.Skip(3));
        return serial.Length > 0 && messageType.Length > 0;
    }
}
