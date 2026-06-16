using IoTPlatform.Api.Contracts;
using IoTPlatform.Application.Devices;
using IoTPlatform.Application.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace IoTPlatform.Api.Controllers;

[ApiController]
[Route("api/v1/devices")]
public sealed class DevicesController(
    IDeviceClaimService deviceClaimService,
    IDeviceManagementService deviceManagementService,
    ICurrentUserTokenService currentUserTokenService) : ControllerBase
{
    [HttpPost("claim")]
    public async Task<ActionResult<DeviceSummaryDto>> Claim(ClaimDeviceRequest request, CancellationToken cancellationToken)
    {
        Guid userId;
        try
        {
            userId = currentUserTokenService.GetUserIdFromBearerToken(Request.Headers.Authorization.ToString());
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or SecurityTokenException)
        {
            return Unauthorized(ProblemDetails("unauthorized", "A valid bearer token is required.", StatusCodes.Status401Unauthorized));
        }

        try
        {
            var result = await deviceClaimService.ClaimAsync(userId, request.ToDto(), cancellationToken);
            return Created($"/api/v1/devices/{result.Id}", result);
        }
        catch (InvalidPairingCodeException exception)
        {
            return NotFound(ProblemDetails("invalid_pairing_code", exception.Message, StatusCodes.Status404NotFound));
        }
        catch (DeviceAlreadyClaimedException exception)
        {
            return Conflict(ProblemDetails("device_already_claimed", exception.Message, StatusCodes.Status409Conflict));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(ProblemDetails("validation_error", exception.Message, StatusCodes.Status400BadRequest));
        }
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DeviceSummaryDto>>> List(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId, out var unauthorized))
        {
            return unauthorized;
        }

        return Ok(await deviceManagementService.ListAsync(userId, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DeviceSummaryDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId, out var unauthorized))
        {
            return unauthorized;
        }

        try
        {
            return Ok(await deviceManagementService.GetAsync(userId, id, cancellationToken));
        }
        catch (DeviceNotFoundException exception)
        {
            return NotFound(ProblemDetails("device_not_found", exception.Message, StatusCodes.Status404NotFound));
        }
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<DeviceSummaryDto>> Update(Guid id, UpdateDeviceRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId, out var unauthorized))
        {
            return unauthorized;
        }

        try
        {
            return Ok(await deviceManagementService.UpdateAsync(userId, id, request.ToDto(), cancellationToken));
        }
        catch (DeviceNotFoundException exception)
        {
            return NotFound(ProblemDetails("device_not_found", exception.Message, StatusCodes.Status404NotFound));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(ProblemDetails("validation_error", exception.Message, StatusCodes.Status400BadRequest));
        }
    }

    [HttpDelete("{id:guid}/claim")]
    public async Task<IActionResult> Unclaim(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId, out var unauthorized))
        {
            return unauthorized;
        }

        try
        {
            await deviceManagementService.UnclaimAsync(userId, id, cancellationToken);
            return NoContent();
        }
        catch (DeviceNotFoundException exception)
        {
            return NotFound(ProblemDetails("device_not_found", exception.Message, StatusCodes.Status404NotFound));
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
