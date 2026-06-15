using System.Data.Common;

namespace IoTPlatform.Infrastructure.Persistence;

public interface IDbConnectionFactory
{
    Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}
