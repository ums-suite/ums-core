using UMS.Modules.Finance.Domain.Common;

namespace UMS.Modules.Finance.Domain.Events;

/// <summary>Consumed by Notifications ("fee due") and Reporting (requirement-spec.md finance §6).</summary>
public sealed record InvoiceGenerated(Guid InvoiceId, string SourceModule, string SourceReferenceId, string FeeType, Guid OwnerId, decimal TotalAmount, DateTimeOffset OccurredAt) : IDomainEvent;
