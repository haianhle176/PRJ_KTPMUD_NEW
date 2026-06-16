namespace IoTPlatform.Application.Commands;

public interface ICommandService
{
    Task<CommandDto> CreateManualAsync(Guid userId, CreateCommandRequestDto request, CancellationToken cancellationToken = default);

    Task<CommandDto> GetAsync(Guid userId, Guid commandId, CancellationToken cancellationToken = default);
}
