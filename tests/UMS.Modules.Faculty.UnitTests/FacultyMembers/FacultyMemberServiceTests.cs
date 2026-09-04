using UMS.Modules.Faculty.Application.Common;
using UMS.Modules.Faculty.Application.FacultyMembers;
using UMS.Modules.Faculty.UnitTests.TestDoubles;

namespace UMS.Modules.Faculty.UnitTests.FacultyMembers;

public sealed class FacultyMemberServiceTests
{
    private static AuditContext Audit(Guid actorUserId) => new(actorUserId, "127.0.0.1", Guid.NewGuid().ToString());

    private static (FacultyMemberService Service, FakeFacultyMemberRepository Repository) CreateService(bool departmentExists = true)
    {
        var repository = new FakeFacultyMemberRepository();
        var service = new FacultyMemberService(repository, new FakeOrganizationDepartmentExistenceChecker(departmentExists), new FakeUnitOfWork(), new FakeAuditRecorder(), new FakeClock());
        return (service, repository);
    }

    [Fact]
    public async Task OnboardAsync_with_a_nonexistent_department_fails()
    {
        var (service, _) = CreateService(departmentExists: false);

        var result = await service.OnboardAsync(new OnboardFacultyMemberRequest(Guid.NewGuid(), "EMP-1", Guid.NewGuid(), Guid.NewGuid(), "FullTime", new DateOnly(2020, 1, 1)), Audit(Guid.NewGuid()));

        Assert.True(result.IsFailure);
        Assert.Equal("department.not_found", result.Error!.Code);
    }

    [Fact]
    public async Task OnboardAsync_the_same_UserId_twice_is_rejected()
    {
        var (service, _) = CreateService();
        var userId = Guid.NewGuid();

        var first = await service.OnboardAsync(new OnboardFacultyMemberRequest(userId, "EMP-1", Guid.NewGuid(), Guid.NewGuid(), "FullTime", new DateOnly(2020, 1, 1)), Audit(Guid.NewGuid()));
        Assert.True(first.IsSuccess);

        var second = await service.OnboardAsync(new OnboardFacultyMemberRequest(userId, "EMP-2", Guid.NewGuid(), Guid.NewGuid(), "FullTime", new DateOnly(2020, 1, 1)), Audit(Guid.NewGuid()));

        Assert.True(second.IsFailure);
        Assert.Equal("facultymember.already_onboarded", second.Error!.Code);
    }

    [Fact]
    public async Task UpdateSelfServiceAsync_by_the_owner_succeeds()
    {
        var (service, _) = CreateService();
        var userId = Guid.NewGuid();
        var onboarded = (await service.OnboardAsync(new OnboardFacultyMemberRequest(userId, "EMP-3", Guid.NewGuid(), Guid.NewGuid(), "FullTime", new DateOnly(2020, 1, 1)), Audit(userId))).Value;

        var result = await service.UpdateSelfServiceAsync(onboarded.Id, userId, new UpdateSelfServiceProfileRequest("me@example.edu.bd", null, onboarded.Version), Audit(userId));

        Assert.True(result.IsSuccess);
        Assert.Equal("me@example.edu.bd", result.Value.ContactEmail);
    }

    [Fact]
    public async Task UpdateSelfServiceAsync_by_a_non_owner_is_forbidden()
    {
        var (service, _) = CreateService();
        var userId = Guid.NewGuid();
        var onboarded = (await service.OnboardAsync(new OnboardFacultyMemberRequest(userId, "EMP-4", Guid.NewGuid(), Guid.NewGuid(), "FullTime", new DateOnly(2020, 1, 1)), Audit(userId))).Value;

        var result = await service.UpdateSelfServiceAsync(onboarded.Id, Guid.NewGuid(), new UpdateSelfServiceProfileRequest("hacker@example.edu.bd", null, onboarded.Version), Audit(userId));

        Assert.True(result.IsFailure);
        Assert.Equal("facultymember.not_owner", result.Error!.Code);
    }
}
