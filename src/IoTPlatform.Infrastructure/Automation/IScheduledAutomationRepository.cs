namespace IoTPlatform.Infrastructure.Automation;

public interface IScheduledAutomationRepository
{
    Task<int> TriggerDueScheduleOnlyRulesAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}
