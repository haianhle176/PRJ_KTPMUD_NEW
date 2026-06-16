using Dapper;
using IoTPlatform.Infrastructure.Configuration;
using IoTPlatform.Infrastructure.Persistence;
using IoTPlatform.Infrastructure.Persistence.Migrations;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace IoTPlatform.Infrastructure.Tests.Persistence;

public sealed class SqlPersistenceIntegrationTests
{
    private const string MasterConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Integrated Security=true;Initial Catalog=master;Encrypt=false;TrustServerCertificate=true";

    [Fact]
    public async Task MigrateAsync_CreatesSchema_IsIdempotent_AndSupportsTransactions()
    {
        var databaseName = $"IoTPlatform_Test_{Guid.NewGuid():N}";
        var databaseConnectionString = new SqlConnectionStringBuilder(MasterConnectionString)
        {
            InitialCatalog = databaseName
        }.ConnectionString;

        await CreateDatabaseAsync(databaseName);

        try
        {
            var connectionFactory = new SqlConnectionFactory(Options.Create(new DatabaseOptions
            {
                IoTPlatform = databaseConnectionString
            }));
            var runner = new SqlMigrationRunner(connectionFactory, new EmbeddedSqlMigrationSource());

            await runner.MigrateAsync();
            await runner.MigrateAsync();

            await using (var connection = await connectionFactory.OpenConnectionAsync())
            {
                var migrationCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.SchemaVersions;");
                var expectedTables = new[]
                {
                    "Users", "RefreshTokens", "Devices", "PairingCodes", "Capabilities",
                    "TelemetryRecords", "DeviceLatestValues", "DeviceCommands", "CommandAcknowledgements",
                    "AutomationRules", "AutomationExecutions", "ProcessedMessages", "AuditLogs",
                    "Notifications", "TelemetryHourlyAggregates"
                };
                var tableCount = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM sys.tables WHERE name IN @Names;",
                    new { Names = expectedTables });

                Assert.Equal(4, migrationCount);
                Assert.Equal(expectedTables.Length, tableCount);
            }

            var transactionFactory = new SqlTransactionScopeFactory(connectionFactory);
            var rolledBackUserId = Guid.NewGuid();
            await using (var scope = await transactionFactory.BeginAsync())
            {
                await InsertUserAsync(scope, rolledBackUserId, "rollback@example.test");
            }

            var committedUserId = Guid.NewGuid();
            await using (var scope = await transactionFactory.BeginAsync())
            {
                await InsertUserAsync(scope, committedUserId, "commit@example.test");
                await scope.CommitAsync();
            }

            await using (var connection = await connectionFactory.OpenConnectionAsync())
            {
                var rolledBackCount = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM dbo.Users WHERE Id = @Id;",
                    new { Id = rolledBackUserId });
                var committedCount = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM dbo.Users WHERE Id = @Id;",
                    new { Id = committedUserId });

                Assert.Equal(0, rolledBackCount);
                Assert.Equal(1, committedCount);
            }
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabaseAsync(databaseName);
        }
    }

    private static Task InsertUserAsync(ISqlTransactionScope scope, Guid userId, string email) =>
        scope.Connection.ExecuteAsync(
            """
            INSERT dbo.Users (Id, Email, NormalizedEmail, DisplayName, PasswordHash)
            VALUES (@Id, @Email, @NormalizedEmail, @DisplayName, @PasswordHash);
            """,
            new
            {
                Id = userId,
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                DisplayName = "Integration Test",
                PasswordHash = "not-a-real-password-hash"
            },
            scope.Transaction);

    private static async Task CreateDatabaseAsync(string databaseName)
    {
        await using var connection = new SqlConnection(MasterConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync($"CREATE DATABASE [{databaseName}];");
    }

    private static async Task DropDatabaseAsync(string databaseName)
    {
        await using var connection = new SqlConnection(MasterConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync($"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];");
    }
}
