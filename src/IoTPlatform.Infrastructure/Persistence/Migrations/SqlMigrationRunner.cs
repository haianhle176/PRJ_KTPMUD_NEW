using System.Data.Common;
using System.Text.RegularExpressions;
using Dapper;

namespace IoTPlatform.Infrastructure.Persistence.Migrations;

public sealed partial class SqlMigrationRunner(
    IDbConnectionFactory connectionFactory,
    ISqlMigrationSource migrationSource) : ISqlMigrationRunner
{
    private const string EnsureHistoryTableSql = """
        IF OBJECT_ID(N'dbo.SchemaVersions', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.SchemaVersions
            (
                Version int NOT NULL CONSTRAINT PK_SchemaVersions PRIMARY KEY,
                Name nvarchar(200) NOT NULL,
                Checksum char(64) NOT NULL,
                AppliedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_SchemaVersions_AppliedAtUtc DEFAULT SYSUTCDATETIME()
            );
        END;
        """;

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(EnsureHistoryTableSql, cancellationToken: cancellationToken));

        foreach (var migration in migrationSource.GetMigrations())
        {
            await ApplyMigrationAsync(connection, migration, cancellationToken);
        }
    }

    private static async Task ApplyMigrationAsync(
        DbConnection connection,
        SqlMigration migration,
        CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var applied = await connection.QuerySingleOrDefaultAsync<AppliedMigration>(new CommandDefinition(
                "SELECT Version, Name, Checksum FROM dbo.SchemaVersions WITH (UPDLOCK, HOLDLOCK) WHERE Version = @Version;",
                new { migration.Version },
                transaction,
                cancellationToken: cancellationToken));

            if (applied is not null)
            {
                if (!string.Equals(applied.Name, migration.Name, StringComparison.Ordinal) ||
                    !string.Equals(applied.Checksum, migration.Checksum, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Applied migration V{migration.Version:000} does not match the embedded script.");
                }

                await transaction.CommitAsync(cancellationToken);
                return;
            }

            foreach (var batch in SplitBatches(migration.Script))
            {
                await connection.ExecuteAsync(new CommandDefinition(batch, transaction: transaction, cancellationToken: cancellationToken));
            }

            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT dbo.SchemaVersions (Version, Name, Checksum) VALUES (@Version, @Name, @Checksum);",
                migration,
                transaction,
                cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    internal static IReadOnlyList<string> SplitBatches(string script) =>
        GoBatchRegex().Split(script)
            .Select(batch => batch.Trim())
            .Where(batch => batch.Length > 0)
            .ToArray();

    private sealed record AppliedMigration(int Version, string Name, string Checksum);

    [GeneratedRegex(@"^\s*GO\s*(?:--.*)?$", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GoBatchRegex();
}
