using System.Security.Cryptography;
using System.Text.Json;
using IoTPlatform.DeviceApp.Configuration;
using IoTPlatform.DeviceApp.Profiles;
using IoTPlatform.Domain.Capabilities;
using IoTPlatform.Domain.Messaging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace IoTPlatform.DeviceApp.Mqtt;

public sealed class SimulatedDeviceSession(
    DeviceHiveMqOptions hiveMq,
    DeviceProfile profile,
    Action<string> log,
    IReadOnlyDictionary<string, JsonElement>? initialActuatorStates = null)
{
    private readonly Dictionary<string, JsonElement> _actuatorStates = initialActuatorStates is null
        ? new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        : new Dictionary<string, JsonElement>(initialActuatorStates, StringComparer.Ordinal);
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly string _pairingCode = CreatePairingCode();
    private readonly SemaphoreSlim _publishLock = new(1, 1);
    private IMqttClient? _client;
    private CancellationTokenSource? _runCancellation;
    private Task? _runTask;
    private TaskCompletionSource? _started;

    public string Serial => profile.Serial;
    public string DeviceType => profile.DeviceType;
    public string PairingCode => _pairingCode;
    public bool IsRunning => _client?.IsConnected == true;

    public async Task StartAsync()
    {
        if (_runTask is not null && !_runTask.IsCompleted)
        {
            if (_started is not null)
            {
                await _started.Task;
            }
            return;
        }

        _runCancellation = new CancellationTokenSource();
        _started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _runTask = RunSafelyAsync(_runCancellation.Token, _started);
        await _started.Task;
    }

    public async Task StopAsync()
    {
        if (_runCancellation is null)
        {
            return;
        }

        _runCancellation.Cancel();
        if (_runTask is not null)
        {
            try
            {
                await _runTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _runCancellation.Dispose();
        _runCancellation = null;
        _runTask = null;
        _started = null;
    }

    private async Task RunSafelyAsync(CancellationToken cancellationToken, TaskCompletionSource started)
    {
        try
        {
            await RunAsync(cancellationToken, started);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            started.TrySetCanceled(cancellationToken);
        }
        catch (Exception exception)
        {
            started.TrySetException(exception);
            log($"{profile.Serial} session stopped: {exception.Message}");
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken, TaskCompletionSource started)
    {
        var factory = new MqttFactory();
        using var client = factory.CreateMqttClient();
        _client = client;
        client.ApplicationMessageReceivedAsync += async eventArgs =>
        {
            if (string.Equals(eventArgs.ApplicationMessage.Topic, MqttTopics.Commands(profile.Serial), StringComparison.Ordinal))
            {
                await HandleCommandAsync(client, eventArgs.ApplicationMessage.ConvertPayloadToString(), cancellationToken);
            }
        };

        var offlinePayload = SerializePresence("offline");
        var options = new MqttClientOptionsBuilder()
            .WithClientId($"device-{profile.Serial}-{_sessionId:N}")
            .WithTcpServer(hiveMq.Host, hiveMq.Port)
            .WithCredentials(hiveMq.Username, hiveMq.Password)
            .WithCleanSession()
            .WithTlsOptions(builder => builder.UseTls())
            .WithWillTopic(MqttTopics.Presence(profile.Serial))
            .WithWillPayload(offlinePayload)
            .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .WithWillRetain()
            .Build();

        try
        {
            await client.ConnectAsync(options, cancellationToken);
            await PublishAsync(client, MqttTopics.Manifest(profile.Serial), SerializeManifest(), retain: true, cancellationToken);
            await PublishAsync(client, MqttTopics.Presence(profile.Serial), SerializePresence("online"), retain: true, cancellationToken);
            await client.SubscribeAsync(MqttTopics.Commands(profile.Serial), MqttQualityOfServiceLevel.AtLeastOnce, cancellationToken);
            log($"{profile.Serial} online. Pairing code {_pairingCode}.");
            started.TrySetResult();

            var nextPresenceAt = DateTimeOffset.UtcNow.AddSeconds(15);
            var nextTelemetryAt = DateTimeOffset.UtcNow;
            while (!cancellationToken.IsCancellationRequested && client.IsConnected)
            {
                if (DateTimeOffset.UtcNow >= nextPresenceAt)
                {
                    await PublishAsync(client, MqttTopics.Presence(profile.Serial), SerializePresence("online"), retain: true, cancellationToken);
                    nextPresenceAt = DateTimeOffset.UtcNow.AddSeconds(15);
                }

                if (DateTimeOffset.UtcNow >= nextTelemetryAt)
                {
                    var readings = CreateAutomaticReadings();
                    if (readings.Count > 0)
                    {
                        await PublishAsync(client, MqttTopics.Telemetry(profile.Serial), SerializeTelemetry(readings), retain: false, cancellationToken);
                        log($"{profile.Serial} telemetry: {string.Join(", ", readings.Select(reading => reading.CapabilityKey))}");
                    }

                    nextTelemetryAt = DateTimeOffset.UtcNow.AddSeconds(5);
                }

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
        finally
        {
            if (client.IsConnected)
            {
                try
                {
                    await PublishAsync(client, MqttTopics.Presence(profile.Serial), offlinePayload, retain: true, CancellationToken.None);
                    await client.DisconnectAsync();
                    log($"{profile.Serial} offline.");
                }
                catch (Exception exception)
                {
                    log($"{profile.Serial} disconnect warning: {exception.Message}");
                }
            }

            _client = null;
        }
    }

    private string SerializeManifest() => JsonSerializer.Serialize(
        new DeviceManifestMessage(
            Guid.NewGuid(),
            profile.Serial,
            DateTimeOffset.UtcNow,
            DeviceMessageSerializer.CurrentSchemaVersion,
            profile.ManifestVersion,
            _pairingCode,
            profile.Capabilities,
            profile.DeviceType),
        DeviceMessageSerializer.Options);

    private string SerializePresence(string status) => JsonSerializer.Serialize(
        new DevicePresenceMessage(Guid.NewGuid(), profile.Serial, DateTimeOffset.UtcNow, DeviceMessageSerializer.CurrentSchemaVersion, status, _sessionId),
        DeviceMessageSerializer.Options);

    private string SerializeTelemetry(IReadOnlyList<TelemetryReadingItem> readings) => JsonSerializer.Serialize(
        new DeviceTelemetryMessage(Guid.NewGuid(), profile.Serial, DateTimeOffset.UtcNow, DeviceMessageSerializer.CurrentSchemaVersion, readings),
        DeviceMessageSerializer.Options);

    private async Task HandleCommandAsync(IMqttClient client, string payload, CancellationToken cancellationToken)
    {
        DeviceCommandMessage? command = null;
        try
        {
            command = JsonSerializer.Deserialize<DeviceCommandMessage>(payload, DeviceMessageSerializer.Options)
                ?? throw new InvalidOperationException("Command payload is empty.");
            var capability = profile.Capabilities.FirstOrDefault(item => string.Equals(item.Key, command.CapabilityKey, StringComparison.Ordinal))
                ?? throw new InvalidOperationException($"Unknown capability '{command.CapabilityKey}'.");
            var definition = capability.ToDefinition();
            if (definition.Kind != CapabilityKind.Actuator || !definition.IsWritable)
            {
                throw new InvalidOperationException($"Capability '{definition.Key}' is not writable.");
            }

            await PublishCommandAckAsync(client, command.CommandId, "accepted", null, cancellationToken);
            _actuatorStates[definition.Key] = command.DesiredValue.Clone();
            await PublishCommandAckAsync(client, command.CommandId, "completed", _actuatorStates[definition.Key], cancellationToken);
            log($"{profile.Serial} command {command.CommandName} completed.");
        }
        catch (Exception exception)
        {
            log($"{profile.Serial} command failed: {exception.Message}");
            if (command is not null)
            {
                await PublishCommandAckAsync(client, command.CommandId, "failed", null, cancellationToken, "device_rejected", exception.Message);
            }
        }
    }

    private Task PublishCommandAckAsync(
        IMqttClient client,
        Guid commandId,
        string status,
        JsonElement? reportedValue,
        CancellationToken cancellationToken,
        string? errorCode = null,
        string? errorMessage = null) =>
        PublishAsync(
            client,
            MqttTopics.CommandAcknowledgements(profile.Serial),
            JsonSerializer.Serialize(
                new DeviceCommandAcknowledgementMessage(
                    Guid.NewGuid(),
                    profile.Serial,
                    DateTimeOffset.UtcNow,
                    DeviceMessageSerializer.CurrentSchemaVersion,
                    commandId,
                    status,
                    reportedValue,
                    errorCode,
                    errorMessage),
                DeviceMessageSerializer.Options),
            retain: false,
            cancellationToken);

    private IReadOnlyList<TelemetryReadingItem> CreateAutomaticReadings()
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

    private static JsonElement CreateAutomaticValue(CapabilityDefinition definition) => definition.DataType switch
    {
        CapabilityDataType.Number => JsonSerializer.SerializeToElement(CreateNumber(definition), DeviceMessageSerializer.Options),
        CapabilityDataType.Boolean => JsonSerializer.SerializeToElement(Random.Shared.Next(0, 2) == 1, DeviceMessageSerializer.Options),
        CapabilityDataType.String => JsonSerializer.SerializeToElement(DateTimeOffset.UtcNow.ToString("O"), DeviceMessageSerializer.Options),
        _ => throw new InvalidOperationException("Unsupported capability data type.")
    };

    private static decimal CreateNumber(CapabilityDefinition definition)
    {
        var min = definition.MinValue ?? 0m;
        var max = definition.MaxValue ?? 100m;
        if (max <= min)
        {
            return min;
        }

        return Math.Round(min + (decimal)Random.Shared.NextDouble() * (max - min), 2);
    }

    private async Task PublishAsync(IMqttClient client, string topic, string payload, bool retain, CancellationToken cancellationToken)
    {
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(retain)
            .Build();

        await _publishLock.WaitAsync(cancellationToken);
        try
        {
            await client.PublishAsync(message, cancellationToken);
        }
        finally
        {
            _publishLock.Release();
        }
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
