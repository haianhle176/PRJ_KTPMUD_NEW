namespace IoTPlatform.Infrastructure.Persistence.Migrations;

public interface ISqlMigrationRunner
{
    Task MigrateAsync(CancellationToken cancellationToken = default);
}
