using UMS.Modules.Reporting.Domain.Common;

namespace UMS.Modules.Reporting.Domain.RegulatoryReports;

/// <summary>requirement-spec.md §3: consumed by Notifications (notify requester with failure reason).</summary>
public sealed record RegulatoryReportRunFailed(Guid RunId, Guid DefinitionId, Guid RequestedByUserId, string ErrorMessage, DateTimeOffset OccurredAt) : IDomainEvent;
