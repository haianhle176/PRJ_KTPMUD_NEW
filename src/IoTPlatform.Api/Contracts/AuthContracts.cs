using IoTPlatform.Application.Auth;

namespace IoTPlatform.Api.Contracts;

public sealed record RegisterRequest(string Email, string Password, string DisplayName)
{
    public RegisterRequestDto ToDto() => new(Email, Password, DisplayName);
}

public sealed record LoginRequest(string Email, string Password)
{
    public LoginRequestDto ToDto() => new(Email, Password);
}

public sealed record RefreshTokenRequest(string RefreshToken)
{
    public RefreshTokenRequestDto ToDto() => new(RefreshToken);
}

public sealed record LogoutRequest(string RefreshToken)
{
    public LogoutRequestDto ToDto() => new(RefreshToken);
}
