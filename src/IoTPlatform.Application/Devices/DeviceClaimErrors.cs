namespace IoTPlatform.Application.Devices;

public abstract class DeviceClaimException(string message) : Exception(message);

public sealed class InvalidPairingCodeException()
    : DeviceClaimException("Pairing code is invalid, expired, or already used.");

public sealed class DeviceAlreadyClaimedException()
    : DeviceClaimException("Device has already been claimed.");

public sealed class DeviceNotFoundException()
    : DeviceClaimException("Device was not found.");
