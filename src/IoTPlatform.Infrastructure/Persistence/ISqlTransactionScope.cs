using System.Data.Common;

namespace IoTPlatform.Infrastructure.Persistence;

public interface ISqlTransactionScope : IAsyncDisposable
{
    DbConnection Connection { get; }
    DbTransaction Transaction { get; }
    Task CommitAsync(CancellationToken cancellationToken = default);
}
