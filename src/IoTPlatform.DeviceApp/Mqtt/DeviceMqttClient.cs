using System.Text.Json;
using System.Security.Cryptography;
using IoTPlatform.DeviceApp.Configuration;
using IoTPlatform.DeviceApp.Profiles;
using IoTPlatform.Domain.Capabilities;
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
        var actuatorStates = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        client.ApplicationMessageReceivedAsync += async eventArgs =>
        {
            var topic = eventArgs.ApplicationMessage.Topic;
            if (!string.Equals(topic, MqttTopics.Commands(profile.Serial), StringComparison.Ordinal))
            {
                return;
            }

            await HandleCommandAsync(client, profile, actuatorStates, eventArgs.ApplicationMessage.ConvertPayloadToString(), cancellationToken);
        };
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
                await client.SubscribeAsync(MqttTopics.Commands(profile.Serial), MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce, cancellationToken);
                Console.WriteLine($"Connected to HiveMQ as {profile.Serial}. Press Ctrl+C to stop.");
                Console.WriteLine($"Pairing code: {pairingCode} (valid for 10 minutes after registration)");
                Console.WriteLine("Telemetry: auto-publishing sensor values every 5 seconds.");
                Console.WriteLine("Manual telemetry command: set <capabilityKey> <value>");
                delaySeconds = _deviceApp.ReconnectMinSeconds;
                var nextTelemetryAt = DateTimeOffset.UtcNow;
                var nextPresenceAt = DateTimeOffset.UtcNow.AddSeconds(15);

                while (client.IsConnected && !cancellationToken.IsCancellationRequested)
                {
                    if (DateTimeOffset.UtcNow >= nextPresenceAt)
                    {
                        await PublishAsync(client, MqttTopics.Presence(profile.Serial), SerializePresence(profile.Serial, sessionId, "online"), retain: true, cancellationToken);
                        nextPresenceAt = DateTimeOffset.UtcNow.AddSeconds(15);
                    }

                    if (DateTimeOffset.UtcNow >= nextTelemetryAt)
                    {
                        await PublishTelemetryAsync(client, profile, CreateAutomaticReadings(profile), cancellationToken);
                        nextTelemetryAt = DateTimeOffset.UtcNow.AddSeconds(5);
                    }

                    string? error = null;
                    if (TryReadConsoleLine(out var line) && TryCreateManualReading(profile, line, out var manualReading, out error))
                    {
                        await PublishTelemetryAsync(client, profile, [manualReading], cancellationToken);
                    }
                    else if (!string.IsNullOrWhiteSpace(error))
                    {
                        Console.WriteLine(error);
                    }

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

    private static string SerializeTelemetry(string serial, IReadOnlyList<TelemetryReadingItem> readings) => JsonSerializer.Serialize(
        new DeviceTelemetryMessage(
            Guid.NewGuid(),
            serial,
            DateTimeOffset.UtcNow,
            DeviceMessageSerializer.CurrentSchemaVersion,
            readings),
        DeviceMessageSerializer.Options);

    private static string SerializeCommandAcknowledgement(
        string serial,
        Guid commandId,
        string status,
        JsonElement? reportedValue,
        string? errorCode = null,
        string? errorMessage = null) => JsonSerializer.Serialize(
        new DeviceCommandAcknowledgementMessage(
            Guid.NewGuid(),
            serial,
            DateTimeOffset.UtcNow,
            DeviceMessageSerializer.CurrentSchemaVersion,
            commandId,
            status,
            reportedValue,
            errorCode,
            errorMessage),
        DeviceMessageSerializer.Options);

    private static Task PublishTelemetryAsync(
        IMqttClient client,
        DeviceProfile profile,
        IReadOnlyList<TelemetryReadingItem> readings,
        CancellationToken cancellationToken)
    {
        if (readings.Count == 0)
        {
            return Task.CompletedTask;
        }

        Console.WriteLine($"Telemetry -> {string.Join(", ", readings.Select(reading => reading.CapabilityKey))}");
        return PublishAsync(client, MqttTopics.Telemetry(profile.Serial), SerializeTelemetry(profile.Serial, readings), retain: false, cancellationToken);
    }

    private static async Task HandleCommandAsync(
        IMqttClient client,
        DeviceProfile profile,
        IDictionary<string, JsonElement> actuatorStates,
        string payload,
        CancellationToken cancellationToken)
    {
        DeviceCommandMessage? command = null;
        try
        {
            command = JsonSerializer.Deserialize<DeviceCommandMessage>(payload, DeviceMessageSerializer.Options)
                ?? throw new InvalidOperationException("Command payload is empty.");
            if (!string.Equals(command.SchemaVersion, DeviceMessageSerializer.CurrentSchemaVersion, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Unsupported command schema version.");
            }

            if (!string.Equals(command.Serial, profile.Serial, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Command serial does not match this device.");
            }

            if (command.ExpiresAtUtc is not null && command.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            {
                throw new InvalidOperationException("Command has expired.");
            }

            var capability = profile.Capabilities.FirstOrDefault(item => string.Equals(item.Key, command.CapabilityKey, StringComparison.Ordinal))
                ?? throw new InvalidOperationException($"Unknown capability '{command.CapabilityKey}'.");
            var definition = capability.ToDefinition();
            if (definition.Kind != CapabilityKind.Actuator || !definition.IsWritable)
            {
                throw new InvalidOperationException($"Capability '{definition.Key}' is not a writable actuator.");
            }

            if (!definition.SupportedCommands.Contains(command.CommandName, StringComparer.Ordinal))
            {
                throw new InvalidOperationException($"Command '{command.CommandName}' is not supported by '{definition.Key}'.");
            }

            ValidateCommandValue(definition, command.DesiredValue);
            await PublishCommandAckAsync(client, profile.Serial, command.CommandId, "accepted", null, cancellationToken);
            actuatorStates[definition.Key] = command.DesiredValue.Clone();
            Console.WriteLine($"Command {command.CommandId}: {definition.Key}={command.DesiredValue}");
            await PublishCommandAckAsync(client, profile.Serial, command.CommandId, "completed", actuatorStates[definition.Key], cancellationToken);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Command failed: {exception.Message}");
            if (command is not null)
            {
                await PublishCommandAckAsync(client, profile.Serial, command.CommandId, "failed", null, cancellationToken, "device_rejected", exception.Message);
            }
        }
    }

    private static Task PublishCommandAckAsync(
        IMqttClient client,
        string serial,
        Guid commandId,
        string status,
        JsonElement? reportedValue,
        CancellationToken cancellationToken,
        string? errorCode = null,
        string? errorMessage = null) =>
        PublishAsync(
            client,
            MqttTopics.CommandAcknowledgements(serial),
            SerializeCommandAcknowledgement(serial, commandId, status, reportedValue, errorCode, errorMessage),
            retain: false,
            cancellationToken);

    private static void ValidateCommandValue(CapabilityDefinition definition, JsonElement value)
    {
        switch (definition.DataType)
        {
            case CapabilityDataType.Number:
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var number))
                {
                    throw new InvalidOperationException($"Capability '{definition.Key}' expects a number command value.");
                }

                if (definition.MinValue is not null && number < definition.MinValue)
                {
                    throw new InvalidOperationException($"Capability '{definition.Key}' command value is below minimum.");
                }

                if (definition.MaxValue is not null && number > definition.MaxValue)
                {
                    throw new InvalidOperationException($"Capability '{definition.Key}' command value is above maximum.");
                }

                break;
            case CapabilityDataType.Boolean:
                if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    throw new InvalidOperationException($"Capability '{definition.Key}' expects a boolean command value.");
                }

                break;
            case CapabilityDataType.String:
                if (value.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidOperationException($"Capability '{definition.Key}' expects a string command value.");
                }

                break;
            default:
                throw new InvalidOperationException("Unsupported actuator data type.");
        }
    }

    private static IReadOnlyList<TelemetryReadingItem> CreateAutomaticReadings(DeviceProfile profile)
    {
        var readings = new List<TelemetryReadingItem>();
        foreach (var capability in profile.Capabilities)
        {
            var definition = capability.ToDefinition();
            if (definition.Kind != CapabilityKind.Sensor)
            {
                continue;
            }

            readings.Add(new TelemetryReadingItem(definition.Key, CreateAutomaticValue(definition)));
        }

        return readings;
    }

    private static JsonElement CreateAutomaticValue(CapabilityDefinition definition)
    {
        return definition.DataType switch
        {
            CapabilityDataType.Number => JsonSerializer.SerializeToElement(CreateNumber(definition), DeviceMessageSerializer.Options),
            CapabilityDataType.Boolean => JsonSerializer.SerializeToElement(Random.Shared.Next(0, 2) == 1, DeviceMessageSerializer.Options),
            CapabilityDataType.String => JsonSerializer.SerializeToElement(DateTimeOffset.UtcNow.ToString("O"), DeviceMessageSerializer.Options),
            _ => throw new InvalidOperationException("Unsupported capability data type.")
        };
    }

    private static decimal CreateNumber(CapabilityDefinition definition)
    {
        var min = definition.MinValue ?? 0m;
        var max = definition.MaxValue ?? 100m;
        if (max <= min)
        {
            return min;
        }

        var sample = min + (decimal)Random.Shared.NextDouble() * (max - min);
        return Math.Round(sample, 2);
    }

    private static bool TryCreateManualReading(
        DeviceProfile profile,
        string? line,
        out TelemetryReadingItem reading,
        out string? error)
    {
        reading = default!;
        error = null;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var parts = line.Split(' ', 3, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 || !string.Equals(parts[0], "set", StringComparison.OrdinalIgnoreCase))
        {
            error = "Use: set <capabilityKey> <value>";
            return false;
        }

        var capability = profile.Capabilities.FirstOrDefault(item => string.Equals(item.Key, parts[1], StringComparison.Ordinal));
        if (capability is null)
        {
            error = $"Unknown capability '{parts[1]}'.";
            return false;
        }

        var definition = capability.ToDefinition();
        if (definition.Kind != CapabilityKind.Sensor)
        {
            error = $"Capability '{definition.Key}' is not a sensor.";
            return false;
        }

        if (!TryParseManualValue(definition, parts[2], out var value, out error))
        {
            return false;
        }

        reading = new TelemetryReadingItem(definition.Key, value);
        return true;
    }

    private static bool TryParseManualValue(
        CapabilityDefinition definition,
        string rawValue,
        out JsonElement value,
        out string? error)
    {
        value = default;
        error = null;

        switch (definition.DataType)
        {
            case CapabilityDataType.Number:
                if (!decimal.TryParse(rawValue, out var number))
                {
                    error = $"Capability '{definition.Key}' expects a number.";
                    return false;
                }

                if (definition.MinValue is not null && number < definition.MinValue)
                {
                    error = $"Capability '{definition.Key}' is below minimum {definition.MinValue}.";
                    return false;
                }

                if (definition.MaxValue is not null && number > definition.MaxValue)
                {
                    error = $"Capability '{definition.Key}' is above maximum {definition.MaxValue}.";
                    return false;
                }

                value = JsonSerializer.SerializeToElement(number, DeviceMessageSerializer.Options);
                return true;

            case CapabilityDataType.Boolean:
                if (!bool.TryParse(rawValue, out var boolean))
                {
                    error = $"Capability '{definition.Key}' expects true or false.";
                    return false;
                }

                value = JsonSerializer.SerializeToElement(boolean, DeviceMessageSerializer.Options);
                return true;

            case CapabilityDataType.String:
                value = JsonSerializer.SerializeToElement(rawValue, DeviceMessageSerializer.Options);
                return true;

            default:
                error = $"Capability '{definition.Key}' has unsupported data type.";
                return false;
        }
    }

    private static bool TryReadConsoleLine(out string? line)
    {
        line = null;
        try
        {
            if (!Console.KeyAvailable)
            {
                return false;
            }

            line = Console.ReadLine();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

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
