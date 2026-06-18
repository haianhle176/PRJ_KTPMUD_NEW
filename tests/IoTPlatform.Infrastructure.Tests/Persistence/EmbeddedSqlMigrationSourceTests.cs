using IoTPlatform.Infrastructure.Persistence.Migrations;

namespace IoTPlatform.Infrastructure.Tests.Persistence;

public sealed class EmbeddedSqlMigrationSourceTests
{
    [Fact]
    public void GetMigrations_ReturnsOrderedVersionedScriptsWithChecksums()
    {
        var migrations = new EmbeddedSqlMigrationSource().GetMigrations();

        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], migrations.Select(migration => migration.Version));
        Assert.All(migrations, migration => Assert.Equal(64, migration.Checksum.Length));
        Assert.All(migrations, migration => Assert.False(string.IsNullOrWhiteSpace(migration.Script)));
    }

    [Fact]
    public void SplitBatches_SeparatesStandaloneGoStatements()
    {
        const string script = "SELECT 1;\r\nGO\r\nSELECT 'GO is data';\r\ngo -- next batch\r\nSELECT 3;";

        var batches = SqlMigrationRunner.SplitBatches(script);

        Assert.Equal(3, batches.Count);
        Assert.Equal("SELECT 1;", batches[0]);
        Assert.Equal("SELECT 'GO is data';", batches[1]);
        Assert.Equal("SELECT 3;", batches[2]);
    }
}
