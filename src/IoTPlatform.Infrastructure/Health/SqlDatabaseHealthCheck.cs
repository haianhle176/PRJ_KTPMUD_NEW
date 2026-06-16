using Dapper;
using IoTPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IoTPlatform.Infrastructure.Health;

public sealed class SqlDatabaseHealthCheck(IDbConnectionFactory connectionFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
            var result = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT 1;",
                cancellationToken: cancellationToken));

            return result == 1
                ? HealthCheckResult.Healthy("SQL database is reachable.")
                : HealthCheckResult.Unhealthy("SQL database returned an unexpected result.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("SQL database is not reachable.", exception);
        }
    }
}
