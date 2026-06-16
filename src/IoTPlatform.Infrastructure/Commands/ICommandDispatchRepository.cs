using IoTPlatform.Domain.Messaging;

namespace IoTPlatform.Infrastructure.Commands;

public interface ICommandDispatchRepository
{
    Task<IReadOnlyList<PendingCommand>> GetPendingAsync(int take, CancellationToken cancellationToken = default);

    Task<bool> MarkSentAsync(Guid commandId, CancellationToken cancellationToken = default);

    Task<CommandAckResult> ApplyAcknowledgementAsync(DeviceCommandAcknowledgementMessage acknowledgement, CancellationToken cancellationToken = default);
}

public sealed record PendingCommand(
    Guid Id,
    string Serial,
    string CapabilityKey,
    string CommandName,
    string DesiredValueType,
    decimal? DesiredNumberValue,
    bool? DesiredBooleanValue,
    string? DesiredStringValue,
    DateTimeOffset ExpiresAtUtc);

public sealed record CommandAckResult(Guid CommandId, string Status, bool WasDuplicate);
