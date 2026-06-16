using IoTPlatform.Application.Audit;

namespace IoTPlatform.Application.Auth;

public sealed class AuthService(
    IAuthRepository repository,
    IPasswordService passwordService,
    ITokenService tokenService,
    IAuditLogRepository? auditLogRepository = null) : IAuthService
{
    public async Task<AuthResultDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
    {
        ValidateRegistration(request);
        var normalizedEmail = NormalizeEmail(request.Email);

        if (await repository.FindUserByNormalizedEmailAsync(normalizedEmail, cancellationToken) is not null)
        {
            throw new DuplicateEmailException(request.Email);
        }

        var user = new UserRecord(
            Guid.NewGuid(),
            request.Email.Trim(),
            normalizedEmail,
            request.DisplayName.Trim(),
            passwordService.HashPassword(request.Password),
            IsActive: true);
        var tokens = tokenService.CreateTokens(user);
        var refreshToken = new RefreshTokenRecord(
            Guid.NewGuid(),
            user.Id,
            tokenService.HashRefreshToken(tokens.RefreshToken),
            tokens.RefreshTokenExpiresAtUtc,
            RevokedAtUtc: null);

        await repository.CreateUserWithRefreshTokenAsync(user, refreshToken, cancellationToken);
        await WriteAuditAsync(user.Id, "auth.register", "user", user.Id.ToString(), cancellationToken: cancellationToken);
        return new AuthResultDto(ToDto(user), tokens);
    }

    public async Task<AuthResultDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await repository.FindUserByNormalizedEmailAsync(normalizedEmail, cancellationToken);

        if (user is null || !user.IsActive || !passwordService.VerifyPassword(user.PasswordHash, request.Password))
        {
            throw new InvalidCredentialsException();
        }

        var tokens = tokenService.CreateTokens(user);
        await repository.StoreRefreshTokenAsync(new RefreshTokenRecord(
            Guid.NewGuid(),
            user.Id,
            tokenService.HashRefreshToken(tokens.RefreshToken),
            tokens.RefreshTokenExpiresAtUtc,
            RevokedAtUtc: null), cancellationToken);
        await WriteAuditAsync(user.Id, "auth.login", "user", user.Id.ToString(), cancellationToken: cancellationToken);
        return new AuthResultDto(ToDto(user), tokens);
    }

    public async Task<AuthResultDto> RefreshAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default)
    {
        var tokenHash = tokenService.HashRefreshToken(request.RefreshToken);
        var user = await repository.FindUserByRefreshTokenHashAsync(tokenHash, cancellationToken);

        if (user is null || !user.IsActive)
        {
            throw new InvalidRefreshTokenException();
        }

        var tokens = tokenService.CreateTokens(user);
        var refreshToken = new RefreshTokenRecord(
            Guid.NewGuid(),
            user.Id,
            tokenService.HashRefreshToken(tokens.RefreshToken),
            tokens.RefreshTokenExpiresAtUtc,
            RevokedAtUtc: null);

        await repository.StoreRefreshTokenAsync(refreshToken, cancellationToken);
        await repository.RevokeRefreshTokenAsync(tokenHash, refreshToken.Id, cancellationToken);
        return new AuthResultDto(ToDto(user), tokens);
    }

    public async Task LogoutAsync(LogoutRequestDto request, CancellationToken cancellationToken = default)
    {
        await repository.RevokeRefreshTokenAsync(tokenService.HashRefreshToken(request.RefreshToken), cancellationToken: cancellationToken);
        await WriteAuditAsync(null, "auth.logout", "refresh_token", null, cancellationToken: cancellationToken);
    }

    public static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    private static AuthenticatedUserDto ToDto(UserRecord user) => new(user.Id, user.Email, user.DisplayName);

    private Task WriteAuditAsync(Guid? userId, string action, string entityType, string? entityId, string? detailsJson = null, CancellationToken cancellationToken = default) =>
        auditLogRepository?.WriteAsync(userId, action, entityType, entityId, detailsJson, cancellationToken) ?? Task.CompletedTask;

    private static void ValidateRegistration(RegisterRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@', StringComparison.Ordinal))
        {
            throw new ArgumentException("A valid email is required.", nameof(request));
        }

        if (request.Password.Length < 8)
        {
            throw new ArgumentException("Password must contain at least 8 characters.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw new ArgumentException("Display name is required.", nameof(request));
        }
    }
}
