namespace UMS.Modules.Library.Application.Abstractions;

/// <summary>LIB-16: the Faculty-side analog of <see cref="IStudentStatusEventSource"/> - polls Faculty's own outbox for <c>FacultyMemberStatusChanged</c> instead of Student's for <c>StudentStatusChanged</c>. Both wire shapes are genuinely different (see <c>Infrastructure.CrossModule.FacultyOutboxEventSource</c>'s own remarks), but the resulting envelope/port shape is identical by design.</summary>
public interface IFacultyStatusEventSource
{
    public Task<IReadOnlyList<FacultyStatusEventEnvelope>> GetUnprocessedAsync(int batchSize, CancellationToken cancellationToken = default);

    public Task MarkProcessedAsync(Guid eventId, CancellationToken cancellationToken = default);
}

/// <summary>Carries only <see cref="FacultyMemberId"/> - see <see cref="StudentStatusEventEnvelope"/>'s own remarks for why the decoded status never crosses the module boundary.</summary>
public sealed record FacultyStatusEventEnvelope(Guid EventId, Guid FacultyMemberId, DateTimeOffset OccurredAt);
