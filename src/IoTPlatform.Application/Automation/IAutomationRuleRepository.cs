using IoTPlatform.Application.Commands;

namespace IoTPlatform.Application.Automation;

public interface IAutomationRuleRepository
{
    Task<AutomationRuleDto?> CreateAsync(
        Guid userId,
        CreateAutomationRuleRequestDto request,
        CommandValue thresholdValue,
        CommandValue desiredValue,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AutomationRuleDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<AutomationRuleDto?> GetAsync(Guid userId, Guid ruleId, CancellationToken cancellationToken = default);

    Task<AutomationRuleDto?> UpdateAsync(
        Guid userId,
        Guid ruleId,
        UpdateAutomationRuleRequestDto request,
        CommandValue thresholdValue,
        CommandValue desiredValue,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid userId, Guid ruleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AutomationExecutionDto>> ListExecutionsAsync(Guid userId, Guid ruleId, int take, CancellationToken cancellationToken = default);
}
