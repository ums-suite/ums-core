namespace UMS.Modules.Research.Application.Abstractions;

/// <summary>RES-5: polls Faculty's own outbox for <c>FacultyMemberStatusChanged</c> - mirrors Library's own <c>IFacultyStatusEventSource</c>/<c>FacultyOutboxEventSource</c> exactly (the most recent, correct precedent for this cross-module outbox-polling shape).</summary>
public interface IFacultyStatusEventSource
{
    public Task<IReadOnlyList<FacultyStatusEventEnvelope>> GetUnprocessedAsync(int batchSize, CancellationToken cancellationToken = default);

    public Task MarkProcessedAsync(Guid eventId, CancellationToken cancellationToken = default);
}

/// <summary>Carries only <see cref="FacultyMemberId"/> - the decoded status never needs to cross the module boundary (RES-5 only cares "this FacultyMember's employment status changed", not to what).</summary>
public sealed record FacultyStatusEventEnvelope(Guid EventId, Guid FacultyMemberId, DateTimeOffset OccurredAt);
