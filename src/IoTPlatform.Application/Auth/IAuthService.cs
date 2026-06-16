namespace IoTPlatform.Application.Auth;

public interface IAuthService
{
    Task<AuthResultDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);

    Task<AuthResultDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);

    Task<AuthResultDto> RefreshAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default);

    Task LogoutAsync(LogoutRequestDto request, CancellationToken cancellationToken = default);
}
