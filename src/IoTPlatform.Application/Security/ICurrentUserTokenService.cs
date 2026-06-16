namespace IoTPlatform.Application.Security;

public interface ICurrentUserTokenService
{
    Guid GetUserIdFromBearerToken(string authorizationHeader);
}
