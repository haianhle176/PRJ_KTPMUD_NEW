using System.Text.Json;
using IoTPlatform.Application.Automation;

namespace IoTPlatform.Api.Contracts;

public sealed record CreateAutomationRuleRequest(
    string Name,
    Guid SourceDeviceId,
    string SourceCapabilityKey,
    string ComparisonOperator,
    JsonElement ThresholdValue,
    Guid TargetDeviceId,
    string TargetCapabilityKey,
    string CommandName,
    JsonElement DesiredValue,
    int CooldownSeconds,
    bool IsEnabled)
{
    public CreateAutomationRuleRequestDto ToDto() => new(
        Name,
        SourceDeviceId,
        SourceCapabilityKey,
        ComparisonOperator,
        ThresholdValue,
        TargetDeviceId,
        TargetCapabilityKey,
        CommandName,
        DesiredValue,
        CooldownSeconds,
        IsEnabled);
}

public sealed record UpdateAutomationRuleRequest(
    string Name,
    string ComparisonOperator,
    JsonElement ThresholdValue,
    string CommandName,
    JsonElement DesiredValue,
    int CooldownSeconds,
    bool IsEnabled)
{
    public UpdateAutomationRuleRequestDto ToDto() => new(Name, ComparisonOperator, ThresholdValue, CommandName, DesiredValue, CooldownSeconds, IsEnabled);
}
