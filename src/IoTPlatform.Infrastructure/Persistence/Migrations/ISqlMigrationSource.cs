namespace IoTPlatform.Infrastructure.Persistence.Migrations;

public interface ISqlMigrationSource
{
    IReadOnlyList<SqlMigration> GetMigrations();
}
