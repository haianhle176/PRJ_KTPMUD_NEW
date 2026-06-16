using System.Text.Json;
using System.Security.Cryptography;
using IoTPlatform.DeviceApp.Configuration;
using IoTPlatform.DeviceApp.Profiles;
using IoTPlatform.Domain.Messaging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace IoTPlatform.DeviceApp.Mqtt;

public sealed class DeviceMqttClient(
    IOptions<DeviceHiveMqOptions> hiveMqOptions,
    IOptions<DeviceAppOptions> deviceAppOptions)
{
    private readonly DeviceHiveMqOptions _hiveMq = hiveMqOptions.Value;
    private readonly DeviceAppOptions _deviceApp = deviceAppOptions.Value;

    public async Task RunAsync(DeviceProfile profile, CancellationToken cancellationToken)
    {
        var sessionId = Guid.NewGuid();
        var pairingCode = CreatePairingCode();
        var factory = new MqttFactory();
        using var client = factory.CreateMqttClient();
        var offlinePayload = SerializePresence(profile.Serial, sessionId, "offline");
        var options = new MqttClientOptionsBuilder()
            .WithClientId($"device-{profile.Serial}-{sessionId:N}")
            .WithTcpServer(_hiveMq.Host, _hiveMq.Port)
            .WithCredentials(_hiveMq.Username, _hiveMq.Password)
            .WithCleanSession()
            .WithTlsOptions(builder => builder.UseTls())
            .WithWillTopic(MqttTopics.Presence(profile.Serial))
            .WithWillPayload(offlinePayload)
            .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .WithWillRetain()
            .Build();

        var delaySeconds = _deviceApp.ReconnectMinSeconds;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await client.ConnectAsync(options, cancellationToken);
                await PublishAsync(client, MqttTopics.Manifest(profile.Serial), SerializeManifest(profile, pairingCode), retain: true, cancellationToken);
                await PublishAsync(client, MqttTopics.Presence(profile.Serial), SerializePresence(profile.Serial, sessionId, "online"), retain: true, cancellationToken);
                Console.WriteLine($"Connected to HiveMQ as {profile.Serial}. Press Ctrl+C to stop.");
                Console.WriteLine($"Pairing code: {pairingCode} (valid for 10 minutes after registration)");
                delaySeconds = _deviceApp.ReconnectMinSeconds;

                while (client.IsConnected && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"MQTT connection failed: {exception.Message}. Retrying in {delaySeconds}s.");
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                delaySeconds = Math.Min(delaySeconds * 2, _deviceApp.ReconnectMaxSeconds);
            }
        }

        if (client.IsConnected)
        {
            await PublishAsync(client, MqttTopics.Presence(profile.Serial), offlinePayload, retain: true, CancellationToken.None);
            await client.DisconnectAsync();
        }
    }

    private static string SerializeManifest(DeviceProfile profile, string pairingCode) => JsonSerializer.Serialize(
        new DeviceManifestMessage(
            Guid.NewGuid(),
            profile.Serial,
            DateTimeOffset.UtcNow,
            DeviceMessageSerializer.CurrentSchemaVersion,
            profile.ManifestVersion,
            pairingCode,
            profile.Capabilities),
        DeviceMessageSerializer.Options);

    private static string SerializePresence(string serial, Guid sessionId, string status) => JsonSerializer.Serialize(
        new DevicePresenceMessage(
            Guid.NewGuid(),
            serial,
            DateTimeOffset.UtcNow,
            DeviceMessageSerializer.CurrentSchemaVersion,
            status,
            sessionId),
        DeviceMessageSerializer.Options);

    private static Task PublishAsync(
        IMqttClient client,
        string topic,
        string payload,
        bool retain,
        CancellationToken cancellationToken)
    {
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(retain)
            .Build();

        return client.PublishAsync(message, cancellationToken);
    }

    private static string CreatePairingCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<char> code = stackalloc char[8];
        for (var index = 0; index < code.Length; index++)
        {
            code[index] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        }

        return new string(code);
    }
}
