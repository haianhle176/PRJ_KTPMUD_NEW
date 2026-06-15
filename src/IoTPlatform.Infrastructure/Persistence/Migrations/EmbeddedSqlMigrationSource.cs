using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace IoTPlatform.Infrastructure.Persistence.Migrations;

public sealed partial class EmbeddedSqlMigrationSource : ISqlMigrationSource
{
    private const string ResourceMarker = ".Persistence.Migrations.Scripts.";
    private readonly Assembly _assembly;

    public EmbeddedSqlMigrationSource()
        : this(typeof(EmbeddedSqlMigrationSource).Assembly)
    {
    }

    internal EmbeddedSqlMigrationSource(Assembly assembly)
    {
        _assembly = assembly;
    }

    public IReadOnlyList<SqlMigration> GetMigrations()
    {
        var migrations = _assembly.GetManifestResourceNames()
            .Where(name => name.Contains(ResourceMarker, StringComparison.Ordinal) && name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .Select(CreateMigration)
            .OrderBy(migration => migration.Version)
            .ToArray();

        if (migrations.Length == 0)
        {
            throw new InvalidOperationException("No embedded SQL migrations were found.");
        }

        if (migrations.Select(migration => migration.Version).Distinct().Count() != migrations.Length)
        {
            throw new InvalidOperationException("SQL migration versions must be unique.");
        }

        return migrations;
    }

    private SqlMigration CreateMigration(string resourceName)
    {
        var fileName = resourceName[(resourceName.LastIndexOf(ResourceMarker, StringComparison.Ordinal) + ResourceMarker.Length)..];
        var match = MigrationFileNameRegex().Match(fileName);

        if (!match.Success)
        {
            throw new InvalidOperationException($"Migration resource '{resourceName}' does not follow VNNN_Name.sql.");
        }

        using var stream = _assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Migration resource '{resourceName}' could not be opened.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var script = reader.ReadToEnd();
        var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(script)));

        return new SqlMigration(
            int.Parse(match.Groups["version"].Value, CultureInfo.InvariantCulture),
            match.Groups["name"].Value,
            script,
            checksum);
    }

    [GeneratedRegex("^V(?<version>[0-9]{3})_(?<name>[A-Za-z0-9_]+)\\.sql$", RegexOptions.CultureInvariant)]
    private static partial Regex MigrationFileNameRegex();
}
