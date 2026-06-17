using System.Text.Json;
using IoTPlatform.Domain.Messaging;
using IoTPlatform.Infrastructure.Devices;
using IoTPlatform.Infrastructure.Telemetry;
using IoTPlatform.Infrastructure.Commands;
using Microsoft.Extensions.Logging;

namespace IoTPlatform.Worker;

public sealed class DeviceMessageHandler(
    ILogger<DeviceMessageHandler> logger,
    IDeviceRegistry deviceRegistry,
    ITelemetryIngestionRepository telemetryIngestionRepository,
    ICommandDispatchRepository commandDispatchRepository)
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
            case "telemetry":
            {
                var telemetry = JsonSerializer.Deserialize<DeviceTelemetryMessage>(payload, DeviceMessageSerializer.Options)
                    ?? throw new JsonException("Telemetry payload is empty.");
                EnsureTopicSerial(topicSerial, telemetry.Serial);
                var result = await telemetryIngestionRepository.IngestAsync(telemetry, topic, cancellationToken);
                if (result.WasDuplicate)
                {
                    logger.LogDebug("Ignored duplicate telemetry message for {Serial}.", telemetry.Serial);
                }
                else
                {
                    logger.LogInformation(
                        "Stored {ReadingCount} telemetry readings for {Serial}.",
                        result.InsertedReadingCount,
                        result.Serial);
                }

                break;
            }
            case "commands/ack":
            {
                var acknowledgement = JsonSerializer.Deserialize<DeviceCommandAcknowledgementMessage>(payload, DeviceMessageSerializer.Options)
                    ?? throw new JsonException("Command acknowledgement payload is empty.");
                EnsureTopicSerial(topicSerial, acknowledgement.Serial);
                ValidateAcknowledgement(acknowledgement);
                var result = await commandDispatchRepository.ApplyAcknowledgementAsync(acknowledgement, cancellationToken);
                if (result.WasDuplicate)
                {
                    logger.LogDebug("Ignored duplicate command ACK {MessageId} for command {CommandId}.", acknowledgement.MessageId, acknowledgement.CommandId);
                }
                else
                {
                    logger.LogInformation("Applied command ACK {Status} for command {CommandId}.", result.Status, result.CommandId);
                }

                break;
            }
            default:
                logger.LogDebug("Ignored unhandled message type {MessageType}.", messageType);
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

    private static void ValidateAcknowledgement(DeviceCommandAcknowledgementMessage acknowledgement)
    {
        if (acknowledgement.MessageId == Guid.Empty || acknowledgement.CommandId == Guid.Empty)
        {
            throw new InvalidOperationException("Command acknowledgement ids are required.");
        }

        if (!string.Equals(acknowledgement.SchemaVersion, DeviceMessageSerializer.CurrentSchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Unsupported command acknowledgement schema version.");
        }

        if (acknowledgement.Status is not ("accepted" or "completed" or "failed"))
        {
            throw new InvalidOperationException("Unsupported command acknowledgement status.");
        }
    }
}
