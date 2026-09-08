using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Alumni.Application.Abstractions;
using UMS.Modules.Alumni.Application.Alumni;
using UMS.Modules.Alumni.IntegrationTests.Infrastructure;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Student;

namespace UMS.Modules.Alumni.IntegrationTests.Alumni;

/// <summary>
/// ALM-1: design-decisions.md "Idempotent StudentGraduated Consumption"; edge-cases.md "Duplicate
/// StudentGraduated delivered concurrently by two consumer instances" - a genuine concurrent race
/// (real <see cref="Task.WhenAll"/>, each branch its own DI scope mirroring a distinct consumer
/// instance) against the real DB-level UNIQUE constraint on <c>student_id_ref</c>, never a
/// sequential retry. Mirrors Research's own <c>GrantConcurrencyTests</c> shape.
/// </summary>
[Collection(AlumniApiTestCollectionDefinition.Name)]
public sealed class StudentGraduatedConsumptionTests(AlumniServiceFixture fixture)
{
    [Fact]
    public async Task Two_concurrent_creation_attempts_for_the_same_StudentId_produce_exactly_one_Alumnus()
    {
        var studentId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;
        fixture.StudentStatusChecker.Register(new StudentAcademicStanding(studentId, Guid.NewGuid(), Guid.NewGuid(), "Graduated"));

        async Task<Result<AlumnusDto>> CreateAsync()
        {
            using var scope = fixture.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<AlumnusService>();
            return await service.CreateFromStudentGraduationAsync(studentId, occurredAt);
        }

        var results = await Task.WhenAll(CreateAsync(), CreateAsync());

        Assert.All(results, r => Assert.True(r.IsSuccess));
        Assert.Equal(results[0].Value.Id, results[1].Value.Id);

        using var verifyScope = fixture.Services.CreateScope();
        var verifyService = verifyScope.ServiceProvider.GetRequiredService<AlumnusService>();
        var byStudentId = await verifyService.GetByStudentIdRefAsync(studentId);
        Assert.True(byStudentId.IsSuccess);
    }

    /// <summary>edge-cases.md "StudentGraduated arrives twice" - a replayed event with a DIFFERENT EventId for the same StudentId (a reconciliation-job replay, not a literal exact-duplicate message) must still be a no-op.</summary>
    [Fact]
    public async Task Duplicate_StudentGraduated_outbox_messages_for_the_same_StudentId_create_exactly_one_Alumnus()
    {
        var studentId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;
        fixture.StudentStatusChecker.Register(new StudentAcademicStanding(studentId, Guid.NewGuid(), Guid.NewGuid(), "Graduated"));

        var payload = $$"""{"StudentId":"{{studentId}}","OccurredAt":"{{occurredAt:O}}"}""";
        await fixture.InsertStudentOutboxMessageAsync(Guid.NewGuid(), "UMS.Modules.Student.Domain.Events.StudentGraduated", payload, occurredAt, occurredAt);
        await fixture.InsertStudentOutboxMessageAsync(Guid.NewGuid(), "UMS.Modules.Student.Domain.Events.StudentGraduated", payload, occurredAt, occurredAt);

        using var scope = fixture.Services.CreateScope();
        var eventSource = scope.ServiceProvider.GetRequiredService<IStudentGraduatedEventSource>();
        var service = scope.ServiceProvider.GetRequiredService<AlumnusService>();

        var envelopes = await eventSource.GetUnprocessedAsync(10);
        Assert.Equal(2, envelopes.Count);

        foreach (var envelope in envelopes)
        {
            var result = await service.CreateFromStudentGraduationAsync(envelope.StudentId, envelope.OccurredAt);
            Assert.True(result.IsSuccess);
            await eventSource.MarkProcessedAsync(envelope.EventId);
        }

        var afterAck = await eventSource.GetUnprocessedAsync(10);
        Assert.Empty(afterAck);

        var byStudentId = await service.GetByStudentIdRefAsync(studentId);
        Assert.True(byStudentId.IsSuccess);
    }
}
