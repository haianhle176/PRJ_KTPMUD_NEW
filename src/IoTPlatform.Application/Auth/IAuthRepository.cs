namespace IoTPlatform.Application.Auth;

public interface IAuthRepository
{
    Task<UserRecord?> FindUserByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    Task<UserRecord?> FindUserByRefreshTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task CreateUserWithRefreshTokenAsync(UserRecord user, RefreshTokenRecord refreshToken, CancellationToken cancellationToken = default);

    Task StoreRefreshTokenAsync(RefreshTokenRecord refreshToken, CancellationToken cancellationToken = default);

    Task RevokeRefreshTokenAsync(string tokenHash, Guid? replacedByTokenId = null, CancellationToken cancellationToken = default);
}
