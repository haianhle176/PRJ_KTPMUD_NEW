using System.Text.Json;
using IoTPlatform.Domain.Messaging;
using IoTPlatform.Infrastructure.Devices;
using IoTPlatform.Infrastructure.Telemetry;
using IoTPlatform.Infrastructure.Commands;
using IoTPlatform.Worker;
using Microsoft.Extensions.Logging.Abstractions;

namespace IoTPlatform.Worker.Tests;

public sealed class DeviceMessageHandlerTests
{
    [Fact]
    public async Task HandleAsync_RoutesValidManifestToRegistry()
    {
        var registry = new RecordingDeviceRegistry();
        var handler = new DeviceMessageHandler(NullLogger<DeviceMessageHandler>.Instance, registry, new RecordingTelemetryIngestionRepository(), new RecordingCommandDispatchRepository());
        var manifest = CreateManifest("DEV-ABCDEF123456");

        await handler.HandleAsync(
            MqttTopics.Manifest(manifest.Serial),
            JsonSerializer.Serialize(manifest, DeviceMessageSerializer.Options));

        Assert.NotNull(registry.Manifest);
        Assert.Equal(manifest.MessageId, registry.Manifest.MessageId);
        Assert.Equal(manifest.Serial, registry.Manifest.Serial);
        Assert.Equal(manifest.ManifestVersion, registry.Manifest.ManifestVersion);
        Assert.Equal(manifest.PairingCode, registry.Manifest.PairingCode);
        var expectedCapability = manifest.Capabilities.Single();
        var actualCapability = registry.Manifest.Capabilities.Single();
        Assert.Equal(expectedCapability.Key, actualCapability.Key);
        Assert.Equal(expectedCapability.DisplayName, actualCapability.DisplayName);
        Assert.Equal(expectedCapability.Kind, actualCapability.Kind);
        Assert.Equal(expectedCapability.DataType, actualCapability.DataType);
        Assert.Equal(expectedCapability.Unit, actualCapability.Unit);
        Assert.Equal(expectedCapability.MinValue, actualCapability.MinValue);
        Assert.Equal(expectedCapability.MaxValue, actualCapability.MaxValue);
        Assert.Equal(expectedCapability.IsWritable, actualCapability.IsWritable);
        Assert.Equal(expectedCapability.SupportedCommands, actualCapability.SupportedCommands);
    }

    [Fact]
    public async Task HandleAsync_RejectsTopicPayloadSerialMismatch()
    {
        var handler = new DeviceMessageHandler(NullLogger<DeviceMessageHandler>.Instance, new RecordingDeviceRegistry(), new RecordingTelemetryIngestionRepository(), new RecordingCommandDispatchRepository());
        var manifest = CreateManifest("DEV-ABCDEF123456");

        var action = () => handler.HandleAsync(
            MqttTopics.Manifest("DEV-DIFFERENT1"),
            JsonSerializer.Serialize(manifest, DeviceMessageSerializer.Options));

        await Assert.ThrowsAsync<InvalidOperationException>(action);
    }

    [Fact]
    public async Task HandleAsync_RoutesTelemetryToIngestionRepository()
    {
        var telemetryRepository = new RecordingTelemetryIngestionRepository();
        var handler = new DeviceMessageHandler(NullLogger<DeviceMessageHandler>.Instance, new RecordingDeviceRegistry(), telemetryRepository, new RecordingCommandDispatchRepository());
        var telemetry = new DeviceTelemetryMessage(
            Guid.NewGuid(),
            "DEV-ABCDEF123456",
            DateTimeOffset.UtcNow,
            DeviceMessageSerializer.CurrentSchemaVersion,
            [new TelemetryReadingItem("temperature", JsonSerializer.SerializeToElement(31.5m, DeviceMessageSerializer.Options))]);

        await handler.HandleAsync(
            MqttTopics.Telemetry(telemetry.Serial),
            JsonSerializer.Serialize(telemetry, DeviceMessageSerializer.Options));

        Assert.NotNull(telemetryRepository.Telemetry);
        Assert.Equal(telemetry.MessageId, telemetryRepository.Telemetry.MessageId);
        Assert.Equal(MqttTopics.Telemetry(telemetry.Serial), telemetryRepository.Topic);
    }

    [Fact]
    public async Task HandleAsync_RoutesCommandAcknowledgementToDispatchRepository()
    {
        var commandDispatch = new RecordingCommandDispatchRepository();
        var handler = new DeviceMessageHandler(NullLogger<DeviceMessageHandler>.Instance, new RecordingDeviceRegistry(), new RecordingTelemetryIngestionRepository(), commandDispatch);
        var acknowledgement = new DeviceCommandAcknowledgementMessage(
            Guid.NewGuid(),
            "DEV-ABCDEF123456",
            DateTimeOffset.UtcNow,
            DeviceMessageSerializer.CurrentSchemaVersion,
            Guid.NewGuid(),
            "completed",
            JsonSerializer.SerializeToElement(true, DeviceMessageSerializer.Options),
            null,
            null);

        await handler.HandleAsync(
            MqttTopics.CommandAcknowledgements(acknowledgement.Serial),
            JsonSerializer.Serialize(acknowledgement, DeviceMessageSerializer.Options));

        Assert.NotNull(commandDispatch.Acknowledgement);
        Assert.Equal(acknowledgement.CommandId, commandDispatch.Acknowledgement.CommandId);
    }

    private static DeviceManifestMessage CreateManifest(string serial) => new(
        Guid.NewGuid(),
        serial,
        DateTimeOffset.UtcNow,
        DeviceMessageSerializer.CurrentSchemaVersion,
        1,
        "ABCD2345",
        [new CapabilityManifestItem("temperature", "Temperature", "sensor", "number", "C", -20, 80, false, [])]);

    private sealed class RecordingDeviceRegistry : IDeviceRegistry
    {
        public DeviceManifestMessage? Manifest { get; private set; }

        public Task<DeviceRegistrationResult> RegisterManifestAsync(DeviceManifestMessage manifest, CancellationToken cancellationToken = default)
        {
            Manifest = manifest;
            return Task.FromResult(new DeviceRegistrationResult(Guid.NewGuid(), null, null));
        }

        public Task UpdatePresenceAsync(DevicePresenceMessage presence, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingTelemetryIngestionRepository : ITelemetryIngestionRepository
    {
        public DeviceTelemetryMessage? Telemetry { get; private set; }

        public string? Topic { get; private set; }

        public Task<TelemetryIngestionResult> IngestAsync(DeviceTelemetryMessage telemetry, string topic, CancellationToken cancellationToken = default)
        {
            Telemetry = telemetry;
            Topic = topic;
            return Task.FromResult(new TelemetryIngestionResult(telemetry.Serial, telemetry.Readings.Count, WasDuplicate: false));
        }
    }

    private sealed class RecordingCommandDispatchRepository : ICommandDispatchRepository
    {
        public DeviceCommandAcknowledgementMessage? Acknowledgement { get; private set; }

        public Task<IReadOnlyList<PendingCommand>> GetPendingAsync(int take, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PendingCommand>>([]);

        public Task<bool> MarkSentAsync(Guid commandId, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<CommandAckResult> ApplyAcknowledgementAsync(DeviceCommandAcknowledgementMessage acknowledgement, CancellationToken cancellationToken = default)
        {
            Acknowledgement = acknowledgement;
            return Task.FromResult(new CommandAckResult(acknowledgement.CommandId, acknowledgement.Status, WasDuplicate: false));
        }
    }
}
