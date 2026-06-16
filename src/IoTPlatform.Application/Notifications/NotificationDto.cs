namespace IoTPlatform.Application.Notifications;

public sealed record NotificationDto(
    long Id,
    Guid OwnerUserId,
    Guid? DeviceId,
    Guid? CapabilityId,
    string Type,
    string Severity,
    string Title,
    string Message,
    bool IsRead,
    DateTimeOffset CreatedAtUtc);
