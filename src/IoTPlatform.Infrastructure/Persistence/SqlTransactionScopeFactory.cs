using System.Data;

namespace IoTPlatform.Infrastructure.Persistence;

public sealed class SqlTransactionScopeFactory(IDbConnectionFactory connectionFactory) : ISqlTransactionScopeFactory
{
    public async Task<ISqlTransactionScope> BeginAsync(CancellationToken cancellationToken = default)
    {
        var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);

        try
        {
            var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            return new SqlTransactionScope(connection, transaction);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
