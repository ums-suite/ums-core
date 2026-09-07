namespace UMS.Modules.Admission.Domain.Publishing;

/// <summary>
/// design-decisions.md "Background Job Design for Bulk Admit-Card and Result-Notification
/// Generation": "one job entity serves both 'is the cache fully warmed' and 'has every applicant's
/// document/notification been fanned out'."
/// </summary>
public enum PublishJobStage
{
    WarmingCache,
    FanningOut,
    Completed,
    Failed,
}

public readonly record struct PublishJobId(Guid Value)
{
    public static PublishJobId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
