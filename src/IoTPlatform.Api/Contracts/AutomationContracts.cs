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
    bool IsEnabled,
    string? ActiveFromTime = null,
    string? ActiveToTime = null,
    int? ActiveDaysOfWeek = null)
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
        IsEnabled,
        ActiveFromTime,
        ActiveToTime,
        ActiveDaysOfWeek);
}

public sealed record UpdateAutomationRuleRequest(
    string Name,
    string ComparisonOperator,
    JsonElement ThresholdValue,
    string CommandName,
    JsonElement DesiredValue,
    int CooldownSeconds,
    bool IsEnabled,
    string? ActiveFromTime = null,
    string? ActiveToTime = null,
    int? ActiveDaysOfWeek = null)
{
    public UpdateAutomationRuleRequestDto ToDto() => new(
        Name,
        ComparisonOperator,
        ThresholdValue,
        CommandName,
        DesiredValue,
        CooldownSeconds,
        IsEnabled,
        ActiveFromTime,
        ActiveToTime,
        ActiveDaysOfWeek);
}
