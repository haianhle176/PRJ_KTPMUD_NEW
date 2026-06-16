namespace IoTPlatform.Application.Automation;

public interface IAutomationRuleService
{
    Task<AutomationRuleDto> CreateAsync(Guid userId, CreateAutomationRuleRequestDto request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AutomationRuleDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<AutomationRuleDto> GetAsync(Guid userId, Guid ruleId, CancellationToken cancellationToken = default);

    Task<AutomationRuleDto> UpdateAsync(Guid userId, Guid ruleId, UpdateAutomationRuleRequestDto request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid userId, Guid ruleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AutomationExecutionDto>> ListExecutionsAsync(Guid userId, Guid ruleId, int take, CancellationToken cancellationToken = default);
}
