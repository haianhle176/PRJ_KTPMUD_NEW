using System.Security.Cryptography;
using System.Text;
using Dapper;
using IoTPlatform.Domain.Messaging;
using IoTPlatform.Infrastructure.Configuration;
using IoTPlatform.Infrastructure.Devices;
using IoTPlatform.Infrastructure.Persistence;
using IoTPlatform.Infrastructure.Persistence.Migrations;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace IoTPlatform.Infrastructure.Tests.Devices;

public sealed class DeviceRegistryIntegrationTests
{
    private const string MasterConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Integrated Security=true;Initial Catalog=master;Encrypt=false;TrustServerCertificate=true";

    [Fact]
    public async Task RegisterManifestAsync_UpsertsCapabilitiesAndStoresOnlyPairingCodeHash()
    {
        var databaseName = $"IoTPlatform_DeviceRegistry_{Guid.NewGuid():N}";
        var connectionString = new SqlConnectionStringBuilder(MasterConnectionString) { InitialCatalog = databaseName }.ConnectionString;
        await ExecuteMasterAsync($"CREATE DATABASE [{databaseName}];");

        try
        {
            var factory = new SqlConnectionFactory(Options.Create(new DatabaseOptions { IoTPlatform = connectionString }));
            await new SqlMigrationRunner(factory, new EmbeddedSqlMigrationSource()).MigrateAsync();
            var registry = new DeviceRegistry(new SqlTransactionScopeFactory(factory));
            const string serial = "DEV-ABCDEF123456";

            await registry.RegisterManifestAsync(CreateManifest(serial, 1, "ABCD2345", [
                Sensor("temperature"),
                Actuator("pump_power")
            ]));
            await registry.RegisterManifestAsync(CreateManifest(serial, 2, "WXYZ6789", [
                Sensor("temperature")
            ], "temperature_sensor"));

            await using var connection = await factory.OpenConnectionAsync();
            var device = await connection.QuerySingleAsync<(int ManifestVersion, string Status, string DeviceType)>(
                "SELECT ManifestVersion, Status, DeviceType FROM dbo.Devices WHERE Serial = @Serial;",
                new { Serial = serial });
            var activeKeys = (await connection.QueryAsync<string>(
                "SELECT [Key] FROM dbo.Capabilities WHERE DeviceId = (SELECT Id FROM dbo.Devices WHERE Serial = @Serial) AND IsActive = 1;",
                new { Serial = serial })).ToArray();
            var inactiveKeys = (await connection.QueryAsync<string>(
                "SELECT [Key] FROM dbo.Capabilities WHERE DeviceId = (SELECT Id FROM dbo.Devices WHERE Serial = @Serial) AND IsActive = 0;",
                new { Serial = serial })).ToArray();
            var pairingCodes = (await connection.QueryAsync<(string CodeHash, DateTimeOffset ExpiresAtUtc)>(
                "SELECT CodeHash, ExpiresAtUtc FROM dbo.PairingCodes WHERE DeviceId = (SELECT Id FROM dbo.Devices WHERE Serial = @Serial) ORDER BY CreatedAtUtc;",
                new { Serial = serial })).ToArray();

            Assert.Equal(2, device.ManifestVersion);
            Assert.Equal("offline", device.Status);
            Assert.Equal("temperature_sensor", device.DeviceType);
            Assert.Equal(["temperature"], activeKeys);
            Assert.Equal(["pump_power"], inactiveKeys);
            Assert.Equal(2, pairingCodes.Length);
            Assert.Equal(Hash("ABCD2345"), pairingCodes[0].CodeHash);
            Assert.Equal(Hash("WXYZ6789"), pairingCodes[1].CodeHash);
            Assert.True(pairingCodes[0].ExpiresAtUtc <= pairingCodes[1].ExpiresAtUtc);
            Assert.DoesNotContain(pairingCodes, code => code.CodeHash.Contains("ABCD2345", StringComparison.Ordinal));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await ExecuteMasterAsync($"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];");
        }
    }

    private static DeviceManifestMessage CreateManifest(
        string serial,
        int version,
        string pairingCode,
        IReadOnlyList<CapabilityManifestItem> capabilities,
        string deviceType = "custom") => new(
            Guid.NewGuid(),
            serial,
            DateTimeOffset.UtcNow,
            DeviceMessageSerializer.CurrentSchemaVersion,
            version,
            pairingCode,
            capabilities,
            deviceType);

    private static CapabilityManifestItem Sensor(string key) =>
        new(key, key, "sensor", "number", "C", -20, 80, false, []);

    private static CapabilityManifestItem Actuator(string key) =>
        new(key, key, "actuator", "boolean", null, null, null, true, ["ON", "OFF"]);

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static async Task ExecuteMasterAsync(string sql)
    {
        await using var connection = new SqlConnection(MasterConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(sql);
    }
}
