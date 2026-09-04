using UMS.Modules.Faculty.Application.CourseAssignments;
using UMS.Modules.Faculty.UnitTests.TestDoubles;

namespace UMS.Modules.Faculty.UnitTests.CourseAssignments;

public sealed class CourseAssignmentProjectionServiceTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.UtcNow;

    [Fact]
    public async Task ApplyInstructorAssignedAsync_with_no_existing_row_creates_an_Active_projection()
    {
        var repository = new FakeCourseAssignmentRepository();
        var service = new CourseAssignmentProjectionService(repository, new FakeUnitOfWork(), new FakeAuditRecorder());
        var payload = new InstructorAssignmentPayload(Guid.NewGuid(), Guid.NewGuid());

        var result = await service.ApplyInstructorAssignedAsync(payload, T0, "corr-1");

        Assert.True(result.IsSuccess);
        var projected = await repository.GetForUpdateAsync(payload.FacultyMemberId, payload.CourseOfferingId);
        Assert.NotNull(projected);
        Assert.Equal(UMS.Modules.Faculty.Domain.CourseAssignments.CourseAssignmentStatus.Active, projected!.Status);
    }

    [Fact]
    public async Task ApplyInstructorUnassignedAsync_with_no_existing_row_is_a_harmless_no_op()
    {
        var repository = new FakeCourseAssignmentRepository();
        var service = new CourseAssignmentProjectionService(repository, new FakeUnitOfWork(), new FakeAuditRecorder());
        var payload = new InstructorAssignmentPayload(Guid.NewGuid(), Guid.NewGuid());

        var result = await service.ApplyInstructorUnassignedAsync(payload, T0, "corr-2");

        Assert.True(result.IsSuccess);
        Assert.Null(await repository.GetForUpdateAsync(payload.FacultyMemberId, payload.CourseOfferingId));
    }

    [Fact]
    public async Task Reapplying_the_same_InstructorAssigned_event_time_is_idempotent()
    {
        var repository = new FakeCourseAssignmentRepository();
        var service = new CourseAssignmentProjectionService(repository, new FakeUnitOfWork(), new FakeAuditRecorder());
        var payload = new InstructorAssignmentPayload(Guid.NewGuid(), Guid.NewGuid());

        await service.ApplyInstructorAssignedAsync(payload, T0, "corr-3");
        var secondResult = await service.ApplyInstructorAssignedAsync(payload, T0, "corr-3-retry");

        Assert.True(secondResult.IsSuccess);
        var projected = await repository.GetForUpdateAsync(payload.FacultyMemberId, payload.CourseOfferingId);
        Assert.Equal(UMS.Modules.Faculty.Domain.CourseAssignments.CourseAssignmentStatus.Active, projected!.Status);
    }
}
