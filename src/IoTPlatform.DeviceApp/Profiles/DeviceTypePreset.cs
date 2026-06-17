using IoTPlatform.Domain.Messaging;

namespace IoTPlatform.DeviceApp.Profiles;

public sealed record DeviceTypePreset(
    string DeviceType,
    string DisplayName,
    string Group,
    string Icon,
    IReadOnlyList<CapabilityManifestItem> Capabilities)
{
    public string IconData => DeviceType switch
    {
        "temperature_sensor" => "M14,4 L14,14.54 A4,4 0 1 1 10,14.54 L10,4 A2,2 0 0 1 14,4 Z",
        "humidity_sensor" => "M12,22 A7,7 0 0 0 19,15 C19,13 18,11.1 16,9.5 C14,7.9 12.5,5.5 12,3 C11.5,5.5 10,7.9 8,9.5 C6,11.1 5,13 5,15 A7,7 0 0 0 12,22 Z",
        "light_sensor" => "M12,8 A4,4 0 1 1 11.99,8 M12,4 L12.01,4 M20,12 L20.01,12 M12,20 L12.01,20 M4,12 L4.01,12 M17.657,6.343 L17.667,6.343 M17.657,17.657 L17.667,17.657 M6.343,17.657 L6.353,17.657 M6.343,6.343 L6.353,6.343",
        "motion_sensor" => "M4,16 L4,13.62 C4,11.5 2.97,10.5 3,8 C3.03,5.28 4.49,2 7.5,2 C9.37,2 10,3.8 10,5.5 C10,8.61 8,11.16 8,14.18 L8,16 A2,2 0 1 1 4,16 Z M20,20 L20,17.62 C20,15.5 21.03,14.5 21,12 C20.97,9.28 19.51,6 16.5,6 C14.63,6 14,7.8 14,9.5 C14,12.61 16,15.16 16,18.18 L16,20 A2,2 0 1 0 20,20 Z M16,17 L20,17 M4,13 L8,13",
        "light_switch" => "M15,14 C15.2,13 15.7,12.3 16.5,11.5 C17.5,10.6 18,9.3 18,8 A6,6 0 0 0 6,8 C6,9 6.2,10.2 7.5,11.5 C8.2,12.2 8.8,13 9,14 M9,18 L15,18 M10,22 L14,22",
        "fan_switch" => "M10.827,16.379 A6.082,6.082 0 0 1 2.209,9.377 L7.621,10.827 A6.082,6.082 0 0 1 14.623,2.209 L13.173,7.621 A6.082,6.082 0 0 1 21.791,14.623 L16.379,13.173 A6.082,6.082 0 0 1 9.377,21.791 Z M12,12 L12,12.01",
        "water_pump" => "M2,7 A2,2 0 0 1 4,5 L13,5 A2,2 0 0 1 15,7 L15,17 A2,2 0 0 1 13,19 L4,19 A2,2 0 0 1 2,17 Z M2,8 A4,4 0 0 0 2,16 M5,8 L12,8 M5,11 L12,11 M5,14 L12,14 M5,16 L12,16 M15,8 L18,9 L18,15 L15,16 M18,10 L20,10 L20,14 L18,14 M20,12 L23,12",
        "door_lock" => "M10,12 L10.01,12 M18,9 L18,6 A2,2 0 0 0 16,4 L8,4 A2,2 0 0 0 6,6 L6,20 M2,20 L10,20 M20,17 L20,15 A2,2 0 1 0 16,15 L16,17 M15,17 L21,17 A1,1 0 0 1 22,18 L22,21 A1,1 0 0 1 21,22 L15,22 A1,1 0 0 1 14,21 L14,18 A1,1 0 0 1 15,17",
        _ => "M8.464,15.536 C6.512,13.583 6.512,10.417 8.464,8.464 M5.636,18.364 C2.121,14.849 2.121,9.151 5.636,5.636 M15.536,15.536 C17.488,13.583 17.488,10.417 15.536,8.464 M18.364,18.364 C21.879,14.849 21.879,9.151 18.364,5.636 M13,12 C13,12.552 12.552,13 12,13 C11.448,13 11,12.552 11,12 C11,11.448 11.448,11 12,11 C12.552,11 13,11.448 13,12"
    };

    public static IReadOnlyList<DeviceTypePreset> All { get; } =
    [
        new("temperature_sensor", "Temperature Sensor", "sensor", "🌡",
            [new CapabilityManifestItem("temperature", "Temperature", "sensor", "number", "C", -20, 80, false, [])]),
        new("humidity_sensor", "Humidity Sensor", "sensor", "💧",
            [new CapabilityManifestItem("humidity", "Humidity", "sensor", "number", "%", 0, 100, false, [])]),
        new("light_sensor", "Light Sensor", "sensor", "☀️",
            [new CapabilityManifestItem("light_level", "Light Level", "sensor", "number", "lux", 0, 100000, false, [])]),
        new("motion_sensor", "Motion Sensor", "sensor", "⌾",
            [new CapabilityManifestItem("motion", "Motion", "sensor", "boolean", null, null, null, false, [])]),
        new("light_switch", "Light Switch", "actuator", "💡",
            [new CapabilityManifestItem("power", "Power", "actuator", "boolean", null, null, null, true, ["ON", "OFF"])]),
        new("fan_switch", "Fan", "actuator", "🌀",
            [new CapabilityManifestItem("power", "Power", "actuator", "boolean", null, null, null, true, ["ON", "OFF"])]),
        new("water_pump", "Water Pump", "actuator", "🚿",
            [new CapabilityManifestItem("power", "Power", "actuator", "boolean", null, null, null, true, ["ON", "OFF"])]),
        new("door_lock", "Door Lock", "actuator", "🔒",
            [new CapabilityManifestItem("lock", "Lock", "actuator", "boolean", null, null, null, true, ["LOCK", "UNLOCK", "ON", "OFF"])])
    ];

    public static DeviceTypePreset Find(string deviceType) =>
        All.FirstOrDefault(item => string.Equals(item.DeviceType, deviceType, StringComparison.OrdinalIgnoreCase)) ?? All[0];
}
