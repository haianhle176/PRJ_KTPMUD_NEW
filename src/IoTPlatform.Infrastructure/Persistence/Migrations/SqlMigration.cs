namespace IoTPlatform.Infrastructure.Persistence.Migrations;

public sealed record SqlMigration(int Version, string Name, string Script, string Checksum);
