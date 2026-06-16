using IoTPlatform.Application.Devices;

namespace IoTPlatform.Api.Contracts;

public sealed record ClaimDeviceRequest(string PairingCode, string DisplayName, string? Category)
{
    public ClaimDeviceRequestDto ToDto() => new(PairingCode, DisplayName, Category);
}

public sealed record UpdateDeviceRequest(string DisplayName, string? Category)
{
    public UpdateDeviceRequestDto ToDto() => new(DisplayName, Category);
}
