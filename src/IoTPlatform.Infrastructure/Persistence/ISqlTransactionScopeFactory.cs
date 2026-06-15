namespace IoTPlatform.Infrastructure.Persistence;

public interface ISqlTransactionScopeFactory
{
    Task<ISqlTransactionScope> BeginAsync(CancellationToken cancellationToken = default);
}
