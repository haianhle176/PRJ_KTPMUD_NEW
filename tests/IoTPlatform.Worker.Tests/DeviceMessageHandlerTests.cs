using System.Text.Json;
using IoTPlatform.Domain.Messaging;
using IoTPlatform.Infrastructure.Devices;
using IoTPlatform.Worker;
using Microsoft.Extensions.Logging.Abstractions;

namespace IoTPlatform.Worker.Tests;

public sealed class DeviceMessageHandlerTests
{
    [Fact]
    public async Task HandleAsync_RoutesValidManifestToRegistry()
    {
        var registry = new RecordingDeviceRegistry();
        var handler = new DeviceMessageHandler(NullLogger<DeviceMessageHandler>.Instance, registry);
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
        var handler = new DeviceMessageHandler(NullLogger<DeviceMessageHandler>.Instance, new RecordingDeviceRegistry());
        var manifest = CreateManifest("DEV-ABCDEF123456");

        var action = () => handler.HandleAsync(
            MqttTopics.Manifest("DEV-DIFFERENT1"),
            JsonSerializer.Serialize(manifest, DeviceMessageSerializer.Options));

        await Assert.ThrowsAsync<InvalidOperationException>(action);
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
}
