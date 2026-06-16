using IoTPlatform.Application.Devices;
using IoTPlatform.Application.Commands;
using System.Text.Json;

namespace IoTPlatform.Api.Contracts;

public sealed record ClaimDeviceRequest(string PairingCode, string DisplayName, string? Category)
{
    public ClaimDeviceRequestDto ToDto() => new(PairingCode, DisplayName, Category);
}

public sealed record UpdateDeviceRequest(string DisplayName, string? Category)
{
    public UpdateDeviceRequestDto ToDto() => new(DisplayName, Category);
}

public sealed record CreateCommandRequest(string CapabilityKey, string CommandName, JsonElement DesiredValue)
{
    public CreateCommandRequestDto ToDto(Guid deviceId) => new(deviceId, CapabilityKey, CommandName, DesiredValue);
}
