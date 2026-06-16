using IoTPlatform.Domain.Messaging;
using IoTPlatform.Infrastructure.Configuration;
using IoTPlatform.Infrastructure.Devices;
using IoTPlatform.Infrastructure.Persistence.Migrations;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace IoTPlatform.Worker;

public sealed class MqttWorker(
    ILogger<MqttWorker> logger,
    IOptions<HiveMqOptions> hiveMqOptions,
    ISqlMigrationRunner migrationRunner,
    DeviceMessageHandler messageHandler) : BackgroundService
{
    private readonly HiveMqOptions _hiveMq = hiveMqOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await migrationRunner.MigrateAsync(stoppingToken);
        logger.LogInformation("Database migrations are current.");

        var delaySeconds = 2;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunMqttSessionAsync(stoppingToken);
                delaySeconds = 2;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "MQTT session failed. Reconnecting in {DelaySeconds} seconds.", delaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
                delaySeconds = Math.Min(delaySeconds * 2, 30);
            }
        }
    }

    private async Task RunMqttSessionAsync(CancellationToken cancellationToken)
    {
        var factory = new MqttFactory();
        using var client = factory.CreateMqttClient();
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        client.ApplicationMessageReceivedAsync += HandleMessageAsync;
        client.DisconnectedAsync += _ =>
        {
            disconnected.TrySetResult();
            return Task.CompletedTask;
        };

        var options = new MqttClientOptionsBuilder()
            .WithClientId($"{_hiveMq.ClientIdPrefix}-{Environment.MachineName}-{Guid.NewGuid():N}")
            .WithTcpServer(_hiveMq.Host, _hiveMq.Port)
            .WithCredentials(_hiveMq.Username, _hiveMq.Password)
            .WithCleanSession()
            .WithTlsOptions(builder => builder.UseTls())
            .Build();

        await client.ConnectAsync(options, cancellationToken);
        var subscribeOptions = factory.CreateSubscribeOptionsBuilder()
            .WithTopicFilter(filter => filter
                .WithTopic("iot/devices/+/manifest")
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
            .WithTopicFilter(filter => filter
                .WithTopic("iot/devices/+/presence")
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
            .Build();
        await client.SubscribeAsync(subscribeOptions, cancellationToken);
        logger.LogInformation("Connected to HiveMQ {Host}:{Port} and subscribed to P1 topics.", _hiveMq.Host, _hiveMq.Port);

        using var registration = cancellationToken.Register(() => disconnected.TrySetCanceled(cancellationToken));
        await disconnected.Task;
    }

    private async Task HandleMessageAsync(MqttApplicationMessageReceivedEventArgs eventArgs)
    {
        var topic = eventArgs.ApplicationMessage.Topic;
        try
        {
            var payload = eventArgs.ApplicationMessage.ConvertPayloadToString();
            await messageHandler.HandleAsync(topic, payload);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to process MQTT message from {Topic}.", topic);
        }
    }

}
