using IoTPlatform.Application.Devices;
using IoTPlatform.Application.Commands;
using System.Text.Json;

namespace IoTPlatform.Api.Contracts;

public sealed record ClaimDeviceRequest(string PairingCode, string DisplayName, string? Category)
{
    public ClaimDeviceRequestDto ToDto() => new(PairingCode, DisplayName, Category);
}

public sealed record ProvisionDeviceRequest(
    string Serial,
    string DisplayName,
    string? Category,
    string? DeviceType,
    int ManifestVersion,
    IReadOnlyList<ProvisionDeviceCapabilityRequest> Capabilities)
{
    public ProvisionDeviceRequestDto ToDto() => new(
        Serial,
        DisplayName,
        Category,
        DeviceType,
        ManifestVersion,
        Capabilities.Select(capability => capability.ToDto()).ToArray());
}

public sealed record ProvisionDeviceCapabilityRequest(
    string Key,
    string DisplayName,
    string Kind,
    string DataType,
    string? Unit,
    decimal? MinValue,
    decimal? MaxValue,
    bool IsWritable,
    IReadOnlyList<string> SupportedCommands)
{
    public ProvisionDeviceCapabilityDto ToDto() => new(
        Key,
        DisplayName,
        Kind,
        DataType,
        Unit,
        MinValue,
        MaxValue,
        IsWritable,
        SupportedCommands);
}

public sealed record UpdateDeviceRequest(string DisplayName, string? Category)
{
    public UpdateDeviceRequestDto ToDto() => new(DisplayName, Category);
}

public sealed record CreateCommandRequest(string CapabilityKey, string CommandName, JsonElement DesiredValue)
{
    public CreateCommandRequestDto ToDto(Guid deviceId) => new(deviceId, CapabilityKey, CommandName, DesiredValue);
}
