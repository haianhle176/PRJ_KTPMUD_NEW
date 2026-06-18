using System.Data.Common;
using IoTPlatform.Infrastructure.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IoTPlatform.Infrastructure.Persistence;

public sealed class SqlConnectionFactory(
    IOptions<DatabaseOptions> options,
    ILogger<SqlConnectionFactory>? logger = null) : IDbConnectionFactory
{
    private const int MaxOpenAttempts = 3;
    private readonly string _connectionString = options.Value.IoTPlatform;

    public async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; attempt <= MaxOpenAttempts; attempt++)
        {
            var connection = new SqlConnection(_connectionString);
            try
            {
                await connection.OpenAsync(cancellationToken);
                return connection;
            }
            catch (SqlException exception) when (attempt < MaxOpenAttempts && IsTransient(exception))
            {
                await connection.DisposeAsync();
                var delay = TimeSpan.FromSeconds(attempt * 2);
                logger?.LogWarning(
                    exception,
                    "SQL connection attempt {Attempt}/{MaxAttempts} failed transiently. Retrying in {DelaySeconds} seconds.",
                    attempt,
                    MaxOpenAttempts,
                    delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }
        }

        throw new InvalidOperationException("SQL connection retry loop completed without returning or throwing.");
    }

    private static bool IsTransient(SqlException exception) => exception.Number is
        -2 or
        20 or
        64 or
        233 or
        10053 or
        10054 or
        10060 or
        40197 or
        40501 or
        40613 or
        49918 or
        49919 or
        49920;
}
