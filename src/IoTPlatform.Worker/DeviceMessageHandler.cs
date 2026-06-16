using System.Text.Json;
using IoTPlatform.Domain.Messaging;
using IoTPlatform.Infrastructure.Devices;

namespace IoTPlatform.Worker;

public sealed class DeviceMessageHandler(
    ILogger<DeviceMessageHandler> logger,
    IDeviceRegistry deviceRegistry)
{
    public async Task HandleAsync(string topic, string payload, CancellationToken cancellationToken = default)
    {
        if (!MqttTopics.TryParse(topic, out var topicSerial, out var messageType))
        {
            throw new InvalidOperationException($"Invalid MQTT topic '{topic}'.");
        }

        switch (messageType)
        {
            case "manifest":
            {
                var manifest = JsonSerializer.Deserialize<DeviceManifestMessage>(payload, DeviceMessageSerializer.Options)
                    ?? throw new JsonException("Manifest payload is empty.");
                EnsureTopicSerial(topicSerial, manifest.Serial);
                await deviceRegistry.RegisterManifestAsync(manifest, cancellationToken);
                logger.LogInformation(
                    "Registered manifest v{ManifestVersion} for {Serial} with {CapabilityCount} capabilities.",
                    manifest.ManifestVersion,
                    manifest.Serial,
                    manifest.Capabilities.Count);
                break;
            }
            case "presence":
            {
                var presence = JsonSerializer.Deserialize<DevicePresenceMessage>(payload, DeviceMessageSerializer.Options)
                    ?? throw new JsonException("Presence payload is empty.");
                EnsureTopicSerial(topicSerial, presence.Serial);
                await deviceRegistry.UpdatePresenceAsync(presence, cancellationToken);
                logger.LogInformation("Device {Serial} is {Status}.", presence.Serial, presence.Status);
                break;
            }
            default:
                logger.LogDebug("Ignored P1-unhandled message type {MessageType}.", messageType);
                break;
        }
    }

    private static void EnsureTopicSerial(string topicSerial, string payloadSerial)
    {
        if (!string.Equals(topicSerial, payloadSerial, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Topic serial and payload serial do not match.");
        }
    }
}
