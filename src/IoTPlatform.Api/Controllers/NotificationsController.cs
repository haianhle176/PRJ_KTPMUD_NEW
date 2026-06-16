using IoTPlatform.Application.Notifications;
using IoTPlatform.Application.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace IoTPlatform.Api.Controllers;

[ApiController]
[Route("api/v1/notifications")]
public sealed class NotificationsController(
    INotificationService notificationService,
    ICurrentUserTokenService currentUserTokenService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> List(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId, out var unauthorized))
        {
            return unauthorized;
        }

        try
        {
            return Ok(await notificationService.ListAsync(userId, unreadOnly, take, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(ProblemDetails("notification_validation_error", exception.Message, StatusCodes.Status400BadRequest));
        }
    }

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId, out var unauthorized))
        {
            return unauthorized;
        }

        try
        {
            await notificationService.MarkReadAsync(userId, id, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException exception)
        {
            return NotFound(ProblemDetails("notification_not_found", exception.Message, StatusCodes.Status404NotFound));
        }
    }

    private bool TryGetUserId(out Guid userId, out ActionResult unauthorized)
    {
        try
        {
            userId = currentUserTokenService.GetUserIdFromBearerToken(Request.Headers.Authorization.ToString());
            unauthorized = Unauthorized();
            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or SecurityTokenException)
        {
            userId = Guid.Empty;
            unauthorized = Unauthorized(ProblemDetails("unauthorized", "A valid bearer token is required.", StatusCodes.Status401Unauthorized));
            return false;
        }
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
