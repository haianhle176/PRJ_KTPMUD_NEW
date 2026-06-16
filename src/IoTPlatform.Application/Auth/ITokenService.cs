namespace IoTPlatform.Application.Auth;

public interface ITokenService
{
    AuthTokensDto CreateTokens(UserRecord user);

    string HashRefreshToken(string refreshToken);
}
