using IoTPlatform.Api.Contracts;
using IoTPlatform.Application.Automation;
using IoTPlatform.Application.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace IoTPlatform.Api.Controllers;

[ApiController]
[Route("api/v1/automation-rules")]
public sealed class AutomationRulesController(
    IAutomationRuleService automationRuleService,
    ICurrentUserTokenService currentUserTokenService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AutomationRuleDto>>> List(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId, out var unauthorized))
        {
            return unauthorized;
        }

        return Ok(await automationRuleService.ListAsync(userId, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<AutomationRuleDto>> Create(CreateAutomationRuleRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId, out var unauthorized))
        {
            return unauthorized;
        }

        try
        {
            var rule = await automationRuleService.CreateAsync(userId, request.ToDto(), cancellationToken);
            return Created($"/api/v1/automation-rules/{rule.Id}", rule);
        }
        catch (AutomationValidationException exception)
        {
            return BadRequest(ProblemDetails("automation_validation_error", exception.Message, StatusCodes.Status400BadRequest));
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AutomationRuleDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId, out var unauthorized))
        {
            return unauthorized;
        }

        try
        {
            return Ok(await automationRuleService.GetAsync(userId, id, cancellationToken));
        }
        catch (AutomationRuleNotFoundException exception)
        {
            return NotFound(ProblemDetails("automation_rule_not_found", exception.Message, StatusCodes.Status404NotFound));
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AutomationRuleDto>> Update(Guid id, UpdateAutomationRuleRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId, out var unauthorized))
        {
            return unauthorized;
        }

        try
        {
            return Ok(await automationRuleService.UpdateAsync(userId, id, request.ToDto(), cancellationToken));
        }
        catch (AutomationRuleNotFoundException exception)
        {
            return NotFound(ProblemDetails("automation_rule_not_found", exception.Message, StatusCodes.Status404NotFound));
        }
        catch (AutomationValidationException exception)
        {
            return BadRequest(ProblemDetails("automation_validation_error", exception.Message, StatusCodes.Status400BadRequest));
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId, out var unauthorized))
        {
            return unauthorized;
        }

        try
        {
            await automationRuleService.DeleteAsync(userId, id, cancellationToken);
            return NoContent();
        }
        catch (AutomationRuleNotFoundException exception)
        {
            return NotFound(ProblemDetails("automation_rule_not_found", exception.Message, StatusCodes.Status404NotFound));
        }
    }

    [HttpGet("{id:guid}/executions")]
    public async Task<ActionResult<IReadOnlyList<AutomationExecutionDto>>> ListExecutions(Guid id, [FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId, out var unauthorized))
        {
            return unauthorized;
        }

        try
        {
            return Ok(await automationRuleService.ListExecutionsAsync(userId, id, take, cancellationToken));
        }
        catch (AutomationValidationException exception)
        {
            return BadRequest(ProblemDetails("automation_validation_error", exception.Message, StatusCodes.Status400BadRequest));
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
