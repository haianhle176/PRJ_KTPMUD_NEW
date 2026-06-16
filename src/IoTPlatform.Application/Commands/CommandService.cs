namespace IoTPlatform.Application.Commands;

public sealed class CommandService(ICommandRepository repository) : ICommandService
{
    public async Task<CommandDto> CreateManualAsync(Guid userId, CreateCommandRequestDto request, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (request.DeviceId == Guid.Empty)
        {
            throw new CommandValidationException("Device id is required.");
        }

        if (string.IsNullOrWhiteSpace(request.CapabilityKey))
        {
            throw new CommandValidationException("Capability key is required.");
        }

        if (string.IsNullOrWhiteSpace(request.CommandName))
        {
            throw new CommandValidationException("Command name is required.");
        }

        var value = CommandValue.FromJson(request.DesiredValue);
        return await repository.CreateManualAsync(
            userId,
            request.DeviceId,
            request.CapabilityKey.Trim(),
            request.CommandName.Trim().ToUpperInvariant(),
            value,
            DateTimeOffset.UtcNow.AddSeconds(30),
            cancellationToken) ?? throw new CommandValidationException("Device or writable capability was not found.");
    }

    public async Task<CommandDto> GetAsync(Guid userId, Guid commandId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        return await repository.GetAsync(userId, commandId, cancellationToken) ?? throw new CommandNotFoundException();
    }
}
