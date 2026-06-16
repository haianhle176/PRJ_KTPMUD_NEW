using System.Text.Json;

namespace IoTPlatform.Application.Automation;

public sealed record CreateAutomationRuleRequestDto(
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
    bool IsEnabled);

public sealed record UpdateAutomationRuleRequestDto(
    string Name,
    string ComparisonOperator,
    JsonElement ThresholdValue,
    string CommandName,
    JsonElement DesiredValue,
    int CooldownSeconds,
    bool IsEnabled);

public sealed record AutomationRuleDto(
    Guid Id,
    string Name,
    Guid SourceDeviceId,
    string SourceDeviceSerial,
    string SourceCapabilityKey,
    string ComparisonOperator,
    string ThresholdValueType,
    decimal? ThresholdNumberValue,
    bool? ThresholdBooleanValue,
    string? ThresholdStringValue,
    Guid TargetDeviceId,
    string TargetDeviceSerial,
    string TargetCapabilityKey,
    string CommandName,
    string DesiredValueType,
    decimal? DesiredNumberValue,
    bool? DesiredBooleanValue,
    string? DesiredStringValue,
    bool IsEnabled,
    int CooldownSeconds,
    bool? LastConditionResult,
    DateTimeOffset? LastTriggeredAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record AutomationExecutionDto(
    long Id,
    Guid AutomationRuleId,
    Guid TriggerMessageId,
    Guid? CommandId,
    string Outcome,
    string? Reason,
    DateTimeOffset EvaluatedAtUtc);
