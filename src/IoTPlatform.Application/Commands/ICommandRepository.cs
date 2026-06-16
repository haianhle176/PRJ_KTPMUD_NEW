using System.Text.Json;

namespace IoTPlatform.Application.Commands;

public interface ICommandRepository
{
    Task<CommandDto?> CreateManualAsync(
        Guid userId,
        Guid deviceId,
        string capabilityKey,
        string commandName,
        CommandValue desiredValue,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default);

    Task<CommandDto?> GetAsync(Guid userId, Guid commandId, CancellationToken cancellationToken = default);
}

public sealed record CommandValue(string ValueType, decimal? NumberValue, bool? BooleanValue, string? StringValue)
{
    public static CommandValue FromJson(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetDecimal(out var number) => new CommandValue("number", number, null, null),
            JsonValueKind.True => new CommandValue("boolean", null, true, null),
            JsonValueKind.False => new CommandValue("boolean", null, false, null),
            JsonValueKind.String => new CommandValue("string", null, null, element.GetString()),
            _ => throw new CommandValidationException("Desired value must be a number, boolean or string.")
        };
    }
}
