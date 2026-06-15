namespace IoTPlatform.Domain.Capabilities;

public sealed record CapabilityDefinition
{
    private CapabilityDefinition(
        string key,
        string displayName,
        CapabilityKind kind,
        CapabilityDataType dataType,
        string? unit,
        decimal? minValue,
        decimal? maxValue,
        bool isWritable,
        IReadOnlyList<string> supportedCommands)
    {
        Key = key;
        DisplayName = displayName;
        Kind = kind;
        DataType = dataType;
        Unit = unit;
        MinValue = minValue;
        MaxValue = maxValue;
        IsWritable = isWritable;
        SupportedCommands = supportedCommands;
    }

    public string Key { get; }
    public string DisplayName { get; }
    public CapabilityKind Kind { get; }
    public CapabilityDataType DataType { get; }
    public string? Unit { get; }
    public decimal? MinValue { get; }
    public decimal? MaxValue { get; }
    public bool IsWritable { get; }
    public IReadOnlyList<string> SupportedCommands { get; }

    public static CapabilityDefinition Create(
        string key,
        string displayName,
        CapabilityKind kind,
        CapabilityDataType dataType,
        string? unit = null,
        decimal? minValue = null,
        decimal? maxValue = null,
        bool isWritable = false,
        IEnumerable<string>? supportedCommands = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        if (key.Length > 64 || !char.IsLower(key[0]) || key.Any(character => !char.IsLower(character) && !char.IsDigit(character) && character != '_'))
        {
            throw new ArgumentException("Capability key must use lower-case letters, digits, or underscores and start with a letter.", nameof(key));
        }

        if (dataType != CapabilityDataType.Number && (minValue is not null || maxValue is not null))
        {
            throw new ArgumentException("Only number capabilities can define minValue or maxValue.");
        }

        if (minValue > maxValue)
        {
            throw new ArgumentException("minValue cannot be greater than maxValue.");
        }

        var commands = (supportedCommands ?? []).Distinct(StringComparer.Ordinal).ToArray();

        if (kind == CapabilityKind.Sensor && (isWritable || commands.Length > 0))
        {
            throw new ArgumentException("Sensor capabilities cannot be writable or declare commands.");
        }

        if (kind == CapabilityKind.Actuator && isWritable && commands.Length == 0)
        {
            throw new ArgumentException("Writable actuator capabilities must declare at least one command.");
        }

        return new CapabilityDefinition(key, displayName, kind, dataType, unit, minValue, maxValue, isWritable, commands);
    }
}
