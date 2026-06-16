using IoTPlatform.Api.Contracts;
using IoTPlatform.Application.Auth;
using Microsoft.AspNetCore.Mvc;

namespace IoTPlatform.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResultDto>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await authService.RegisterAsync(request.ToDto(), cancellationToken);
            return Created($"/api/v1/users/{result.User.Id}", result);
        }
        catch (DuplicateEmailException exception)
        {
            return Conflict(ProblemDetails("email_conflict", exception.Message, StatusCodes.Status409Conflict));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(ProblemDetails("validation_error", exception.Message, StatusCodes.Status400BadRequest));
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResultDto>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await authService.LoginAsync(request.ToDto(), cancellationToken));
        }
        catch (InvalidCredentialsException exception)
        {
            return Unauthorized(ProblemDetails("invalid_credentials", exception.Message, StatusCodes.Status401Unauthorized));
        }
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResultDto>> Refresh(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await authService.RefreshAsync(request.ToDto(), cancellationToken));
        }
        catch (InvalidRefreshTokenException exception)
        {
            return Unauthorized(ProblemDetails("invalid_refresh_token", exception.Message, StatusCodes.Status401Unauthorized));
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(request.ToDto(), cancellationToken);
        return NoContent();
    }

    private ProblemDetails ProblemDetails(string code, string detail, int status) => new()
    {
        Type = $"https://iot-platform.local/problems/{code}",
        Title = detail,
        Detail = detail,
        Status = status,
        Extensions = { ["code"] = code, ["traceId"] = HttpContext.TraceIdentifier }
    };
}
