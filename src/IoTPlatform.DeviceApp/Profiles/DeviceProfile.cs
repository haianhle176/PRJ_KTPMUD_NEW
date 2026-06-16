using IoTPlatform.Domain.Messaging;

namespace IoTPlatform.DeviceApp.Profiles;

public sealed record DeviceProfile(
    string Serial,
    int ManifestVersion,
    IReadOnlyList<CapabilityManifestItem> Capabilities);
