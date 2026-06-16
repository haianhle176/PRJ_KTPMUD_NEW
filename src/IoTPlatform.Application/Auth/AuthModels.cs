namespace IoTPlatform.Application.Auth;

public sealed record AuthenticatedUserDto(Guid Id, string Email, string DisplayName);

public sealed record AuthTokensDto(string AccessToken, DateTimeOffset AccessTokenExpiresAtUtc, string RefreshToken, DateTimeOffset RefreshTokenExpiresAtUtc);

public sealed record AuthResultDto(AuthenticatedUserDto User, AuthTokensDto Tokens);

public sealed record RegisterRequestDto(string Email, string Password, string DisplayName);

public sealed record LoginRequestDto(string Email, string Password);

public sealed record RefreshTokenRequestDto(string RefreshToken);

public sealed record LogoutRequestDto(string RefreshToken);

public sealed record UserRecord(Guid Id, string Email, string NormalizedEmail, string DisplayName, string PasswordHash, bool IsActive);

public sealed record RefreshTokenRecord(Guid Id, Guid UserId, string TokenHash, DateTimeOffset ExpiresAtUtc, DateTimeOffset? RevokedAtUtc);
