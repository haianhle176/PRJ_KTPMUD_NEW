using Dapper;
using IoTPlatform.Application.Auth;
using IoTPlatform.Infrastructure.Persistence;

namespace IoTPlatform.Infrastructure.Auth;

public sealed class SqlAuthRepository(ISqlTransactionScopeFactory transactionScopeFactory, IDbConnectionFactory connectionFactory)
    : IAuthRepository
{
    public async Task<UserRecord?> FindUserByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<UserRecord>(new CommandDefinition(
            """
            SELECT Id, Email, NormalizedEmail, DisplayName, PasswordHash, IsActive
            FROM dbo.Users
            WHERE NormalizedEmail = @NormalizedEmail;
            """,
            new { NormalizedEmail = normalizedEmail },
            cancellationToken: cancellationToken));
    }

    public async Task<UserRecord?> FindUserByRefreshTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<UserRecord>(new CommandDefinition(
            """
            SELECT u.Id, u.Email, u.NormalizedEmail, u.DisplayName, u.PasswordHash, u.IsActive
            FROM dbo.RefreshTokens rt
            INNER JOIN dbo.Users u ON u.Id = rt.UserId
            WHERE rt.TokenHash = @TokenHash
              AND rt.RevokedAtUtc IS NULL
              AND rt.ExpiresAtUtc > SYSUTCDATETIME();
            """,
            new { TokenHash = tokenHash },
            cancellationToken: cancellationToken));
    }

    public async Task CreateUserWithRefreshTokenAsync(UserRecord user, RefreshTokenRecord refreshToken, CancellationToken cancellationToken = default)
    {
        await using var scope = await transactionScopeFactory.BeginAsync(cancellationToken);
        await scope.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT dbo.Users (Id, Email, NormalizedEmail, DisplayName, PasswordHash, IsActive)
            VALUES (@Id, @Email, @NormalizedEmail, @DisplayName, @PasswordHash, @IsActive);
            """,
            user,
            scope.Transaction,
            cancellationToken: cancellationToken));
        await InsertRefreshTokenAsync(scope, refreshToken, cancellationToken);
        await scope.CommitAsync(cancellationToken);
    }

    public async Task StoreRefreshTokenAsync(RefreshTokenRecord refreshToken, CancellationToken cancellationToken = default)
    {
        await using var scope = await transactionScopeFactory.BeginAsync(cancellationToken);
        await InsertRefreshTokenAsync(scope, refreshToken, cancellationToken);
        await scope.CommitAsync(cancellationToken);
    }

    public async Task RevokeRefreshTokenAsync(string tokenHash, Guid? replacedByTokenId = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dbo.RefreshTokens
            SET RevokedAtUtc = COALESCE(RevokedAtUtc, SYSUTCDATETIME()),
                ReplacedByTokenId = COALESCE(@ReplacedByTokenId, ReplacedByTokenId)
            WHERE TokenHash = @TokenHash;
            """,
            new { TokenHash = tokenHash, ReplacedByTokenId = replacedByTokenId },
            cancellationToken: cancellationToken));
    }

    private static Task InsertRefreshTokenAsync(ISqlTransactionScope scope, RefreshTokenRecord refreshToken, CancellationToken cancellationToken) =>
        scope.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT dbo.RefreshTokens (Id, UserId, TokenHash, ExpiresAtUtc, RevokedAtUtc)
            VALUES (@Id, @UserId, @TokenHash, @ExpiresAtUtc, @RevokedAtUtc);
            """,
            refreshToken,
            scope.Transaction,
            cancellationToken: cancellationToken));
}
