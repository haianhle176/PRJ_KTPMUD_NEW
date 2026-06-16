using System.Text.Json;
using IoTPlatform.Domain.Capabilities;

namespace IoTPlatform.Domain.Messaging;

public sealed record DeviceManifestMessage(
    Guid MessageId,
    string Serial,
    DateTimeOffset TimestampUtc,
    string SchemaVersion,
    int ManifestVersion,
    string PairingCode,
    IReadOnlyList<CapabilityManifestItem> Capabilities);

public sealed record CapabilityManifestItem(
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
    public CapabilityDefinition ToDefinition()
    {
        if (!Enum.TryParse<CapabilityKind>(Kind, true, out var kind))
        {
            throw new ArgumentException($"Unsupported capability kind '{Kind}'.", nameof(Kind));
        }

        if (!Enum.TryParse<CapabilityDataType>(DataType, true, out var dataType))
        {
            throw new ArgumentException($"Unsupported capability data type '{DataType}'.", nameof(DataType));
        }

        return CapabilityDefinition.Create(
            Key,
            DisplayName,
            kind,
            dataType,
            Unit,
            MinValue,
            MaxValue,
            IsWritable,
            SupportedCommands);
    }

    public static CapabilityManifestItem FromDefinition(CapabilityDefinition definition) => new(
        definition.Key,
        definition.DisplayName,
        definition.Kind.ToString().ToLowerInvariant(),
        definition.DataType.ToString().ToLowerInvariant(),
        definition.Unit,
        definition.MinValue,
        definition.MaxValue,
        definition.IsWritable,
        definition.SupportedCommands);
}

public sealed record DevicePresenceMessage(
    Guid MessageId,
    string Serial,
    DateTimeOffset TimestampUtc,
    string SchemaVersion,
    string Status,
    Guid SessionId);

public static class DeviceMessageSerializer
{
    public const string CurrentSchemaVersion = "1.0";

    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };
}
