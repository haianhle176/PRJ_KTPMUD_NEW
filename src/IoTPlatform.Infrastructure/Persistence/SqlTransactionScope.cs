using System.Data.Common;

namespace IoTPlatform.Infrastructure.Persistence;

internal sealed class SqlTransactionScope(DbConnection connection, DbTransaction transaction) : ISqlTransactionScope
{
    private bool _committed;
    private bool _disposed;

    public DbConnection Connection { get; } = connection;
    public DbTransaction Transaction { get; } = transaction;

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await Transaction.CommitAsync(cancellationToken);
        _committed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (!_committed)
        {
            await Transaction.RollbackAsync();
        }

        await Transaction.DisposeAsync();
        await Connection.DisposeAsync();
        _disposed = true;
    }
}
