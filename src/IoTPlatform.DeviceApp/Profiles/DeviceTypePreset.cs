using IoTPlatform.Domain.Messaging;

namespace IoTPlatform.DeviceApp.Profiles;

public sealed record DeviceTypePreset(
    string DeviceType,
    string DisplayName,
    string Group,
    string Icon,
    IReadOnlyList<CapabilityManifestItem> Capabilities)
{
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
