using System.Text.Json;
using IoTPlatform.Domain.Devices;
using IoTPlatform.Domain.Messaging;

namespace IoTPlatform.DeviceApp.Profiles;

public sealed class DeviceProfileStore(string rootDirectory)
{
    public async Task<DeviceProfile> LoadOrCreateAsync(
        string profileName,
        Func<string, Task<DeviceProfile>> createProfile,
        bool reconfigure = false,
        CancellationToken cancellationToken = default)
    {
        var profileDirectory = Path.Combine(rootDirectory, SanitizeProfileName(profileName));
        var profilePath = Path.Combine(profileDirectory, "device-profile.json");

        if (File.Exists(profilePath))
        {
            DeviceProfile? existing;
            await using (var readStream = File.OpenRead(profilePath))
            {
                existing = await JsonSerializer.DeserializeAsync<DeviceProfile>(
                    readStream,
                    DeviceMessageSerializer.Options,
                    cancellationToken);
            }

            var validated = Validate(existing ?? throw new InvalidDataException("Device profile is empty."));
            if (!reconfigure)
            {
                return validated;
            }

            var replacement = Validate(await createProfile(validated.Serial)) with
            {
                Serial = validated.Serial,
                ManifestVersion = validated.ManifestVersion + 1
            };
            await SaveAsync(profilePath, replacement, cancellationToken);
            return replacement;
        }

        Directory.CreateDirectory(profileDirectory);
        var created = Validate(await createProfile(DeviceSerial.Create()));
        await SaveAsync(profilePath, created, cancellationToken);
        return created;
    }

    private static async Task SaveAsync(string profilePath, DeviceProfile profile, CancellationToken cancellationToken)
    {
        var temporaryPath = profilePath + ".tmp";
        await using (var writeStream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(writeStream, profile, DeviceMessageSerializer.Options, cancellationToken);
        }

        File.Move(temporaryPath, profilePath, overwrite: true);
    }

    private static DeviceProfile Validate(DeviceProfile profile)
    {
        if (!DeviceSerial.IsValid(profile.Serial) || profile.ManifestVersion < 1 || profile.Capabilities.Count == 0)
        {
            throw new InvalidDataException("Device profile contains invalid identity or manifest metadata.");
        }

        var definitions = profile.Capabilities.Select(capability => capability.ToDefinition()).ToArray();
        if (definitions.Select(definition => definition.Key).Distinct(StringComparer.Ordinal).Count() != definitions.Length)
        {
            throw new InvalidDataException("Device profile contains duplicate capability keys.");
        }

        return profile;
    }

    private static string SanitizeProfileName(string profileName)
    {
        var sanitized = new string(profileName
            .Trim()
            .Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-')
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "default" : sanitized;
    }
}
