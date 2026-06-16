using IoTPlatform.Application.Commands;

namespace IoTPlatform.Application.Automation;

public sealed class AutomationRuleService(IAutomationRuleRepository repository) : IAutomationRuleService
{
    private static readonly HashSet<string> Operators = [">", ">=", "<", "<=", "==", "!="];

    public async Task<AutomationRuleDto> CreateAsync(Guid userId, CreateAutomationRuleRequestDto request, CancellationToken cancellationToken = default)
    {
        ValidateUser(userId);
        ValidateCommon(request.Name, request.ComparisonOperator, request.CooldownSeconds, request.CommandName);
        ValidateSchedule(request.ActiveFromTime, request.ActiveToTime, request.ActiveDaysOfWeek);
        var threshold = CommandValue.FromJson(request.ThresholdValue);
        var desired = CommandValue.FromJson(request.DesiredValue);
        return await repository.CreateAsync(userId, request, threshold, desired, cancellationToken)
            ?? throw new AutomationValidationException("Automation source or target capability is invalid.");
    }

    public Task<IReadOnlyList<AutomationRuleDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        ValidateUser(userId);
        return repository.ListAsync(userId, cancellationToken);
    }

    public async Task<AutomationRuleDto> GetAsync(Guid userId, Guid ruleId, CancellationToken cancellationToken = default)
    {
        ValidateUser(userId);
        return await repository.GetAsync(userId, ruleId, cancellationToken) ?? throw new AutomationRuleNotFoundException();
    }

    public async Task<AutomationRuleDto> UpdateAsync(Guid userId, Guid ruleId, UpdateAutomationRuleRequestDto request, CancellationToken cancellationToken = default)
    {
        ValidateUser(userId);
        ValidateCommon(request.Name, request.ComparisonOperator, request.CooldownSeconds, request.CommandName);
        ValidateSchedule(request.ActiveFromTime, request.ActiveToTime, request.ActiveDaysOfWeek);
        var threshold = CommandValue.FromJson(request.ThresholdValue);
        var desired = CommandValue.FromJson(request.DesiredValue);
        return await repository.UpdateAsync(userId, ruleId, request, threshold, desired, cancellationToken)
            ?? throw new AutomationRuleNotFoundException();
    }

    public async Task DeleteAsync(Guid userId, Guid ruleId, CancellationToken cancellationToken = default)
    {
        ValidateUser(userId);
        if (!await repository.DeleteAsync(userId, ruleId, cancellationToken))
        {
            throw new AutomationRuleNotFoundException();
        }
    }

    public Task<IReadOnlyList<AutomationExecutionDto>> ListExecutionsAsync(Guid userId, Guid ruleId, int take, CancellationToken cancellationToken = default)
    {
        ValidateUser(userId);
        if (take is < 1 or > 200)
        {
            throw new AutomationValidationException("Take must be between 1 and 200.");
        }

        return repository.ListExecutionsAsync(userId, ruleId, take, cancellationToken);
    }

    private static void ValidateUser(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }
    }

    private static void ValidateCommon(string name, string comparisonOperator, int cooldownSeconds, string commandName)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new AutomationValidationException("Rule name is required.");
        }

        if (!Operators.Contains(comparisonOperator))
        {
            throw new AutomationValidationException("Comparison operator is invalid.");
        }

        if (cooldownSeconds < 0)
        {
            throw new AutomationValidationException("Cooldown seconds cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(commandName))
        {
            throw new AutomationValidationException("Command name is required.");
        }
    }

    private static void ValidateSchedule(string? activeFromTime, string? activeToTime, int? activeDaysOfWeek)
    {
        if (!string.IsNullOrWhiteSpace(activeFromTime) && !TimeOnly.TryParse(activeFromTime, out _))
        {
            throw new AutomationValidationException("Active from time must be a valid time, for example 08:00.");
        }

        if (!string.IsNullOrWhiteSpace(activeToTime) && !TimeOnly.TryParse(activeToTime, out _))
        {
            throw new AutomationValidationException("Active to time must be a valid time, for example 18:00.");
        }

        if (activeDaysOfWeek is < 1 or > 127)
        {
            throw new AutomationValidationException("Active days of week must be a bit mask between 1 and 127.");
        }
    }
}
