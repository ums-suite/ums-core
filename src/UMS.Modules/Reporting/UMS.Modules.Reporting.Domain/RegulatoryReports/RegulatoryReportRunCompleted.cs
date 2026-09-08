using UMS.Modules.Reporting.Domain.Common;

namespace UMS.Modules.Reporting.Domain.RegulatoryReports;

/// <summary>requirement-spec.md §3: consumed by Notifications (notify the requesting Admin) and Reporting's own run-status endpoint.</summary>
public sealed record RegulatoryReportRunCompleted(Guid RunId, Guid DefinitionId, Guid RequestedByUserId, DateTimeOffset OccurredAt) : IDomainEvent;
