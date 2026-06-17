using IoTPlatform.Worker;
using IoTPlatform.Infrastructure.Configuration;
using IoTPlatform.Infrastructure.Persistence;
using IoTPlatform.Infrastructure.Persistence.Migrations;
using System.Data.Common;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddHiveMqOptions(builder.Configuration);
builder.Services.AddSingleton<DeviceMessageHandler>();
builder.Services.AddHostedService<MqttWorker>();
builder.Services.AddHostedService<ScheduledAutomationWorker>();
builder.Services.AddHostedService<OperationsMaintenanceWorker>();

var host = builder.Build();

if (args.Any(argument => string.Equals(argument, "--verify-p1", StringComparison.OrdinalIgnoreCase)))
{
    await VerifyP1Async(host.Services);
    return;
}

await host.RunAsync();

static async Task VerifyP1Async(IServiceProvider services)
{
    await services.GetRequiredService<ISqlMigrationRunner>().MigrateAsync();
    await using var connection = await services.GetRequiredService<IDbConnectionFactory>().OpenConnectionAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = """
        SET NOCOUNT ON;
        SELECT d.Serial, d.Status, d.ManifestVersion, COUNT(c.Id) AS ActiveCapabilities
        FROM dbo.Devices d
        LEFT JOIN dbo.Capabilities c ON c.DeviceId = d.Id AND c.IsActive = 1
        WHERE d.Serial IN ('DEV-P1ENV000001','DEV-P1PUMP00001','DEV-P1ROOM00001')
        GROUP BY d.Serial, d.Status, d.ManifestVersion
        ORDER BY d.Serial;
        """;

    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        Console.WriteLine("{0}|{1}|v{2}|capabilities={3}",
            reader.GetString(0),
            reader.GetString(1),
            reader.GetInt32(2),
            reader.GetInt32(3));
    }
}
