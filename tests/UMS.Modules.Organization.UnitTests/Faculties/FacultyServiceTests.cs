using UMS.Modules.Organization.Application.Common;
using UMS.Modules.Organization.Application.Faculties;
using UMS.Modules.Organization.Application.Hierarchy;
using UMS.Modules.Organization.Domain.Campuses;
using UMS.Modules.Organization.Domain.Departments;
using UMS.Modules.Organization.Domain.Faculties;
using UMS.Modules.Organization.UnitTests.TestDoubles;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Organization.UnitTests.Faculties;

/// <summary>requirement-spec.md organization §4 "Deactivation cascades are blocked, not silent."</summary>
public class FacultyServiceTests
{
    private static readonly AuditContext _audit = new(Guid.NewGuid(), "127.0.0.1", "test-correlation");

    private static FacultyService BuildService(FakeFacultyRepository faculties, FakeDepartmentRepository departments, FakeAuditRecorder? auditRecorder = null) =>
        new(
            faculties,
            new FakeCampusRepository(),
            departments,
            new FakeUnitOfWork(),
            auditRecorder ?? new FakeAuditRecorder(),
            new FakeOrganizationTreeCache(),
            new HierarchyAncestryResolver(new FakeCampusRepository(), faculties, departments, new FakeProgramRepository()),
            new FakeClock());

    [Fact]
    public async Task DeactivateAsync_rejects_a_Faculty_with_an_active_Department()
    {
        var faculty = Faculty.Create(CampusId.New(), "Faculty of Engineering", DateTimeOffset.UtcNow);
        var department = Department.Create(faculty.Id, "Computer Science", DateTimeOffset.UtcNow);

        var faculties = new FakeFacultyRepository();
        faculties.Seed(faculty);
        var departments = new FakeDepartmentRepository();
        departments.Seed(department);

        var service = BuildService(faculties, departments);

        var result = await service.DeactivateAsync(faculty.Id.Value, faculty.Version, _audit);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal("faculty.has_active_children", result.Error.Code);
    }

    [Fact]
    public async Task DeactivateAsync_succeeds_once_every_Department_beneath_it_is_inactive()
    {
        var faculty = Faculty.Create(CampusId.New(), "Faculty of Engineering", DateTimeOffset.UtcNow);
        var department = Department.Create(faculty.Id, "Computer Science", DateTimeOffset.UtcNow);
        department.Deactivate(DateTimeOffset.UtcNow);

        var faculties = new FakeFacultyRepository();
        faculties.Seed(faculty);
        var departments = new FakeDepartmentRepository();
        departments.Seed(department);
        var auditRecorder = new FakeAuditRecorder();

        var service = BuildService(faculties, departments, auditRecorder);

        var result = await service.DeactivateAsync(faculty.Id.Value, faculty.Version, _audit);

        Assert.True(result.IsSuccess);
        Assert.Equal("Inactive", result.Value.Status);
        Assert.Contains(auditRecorder.RecordedEntries, e => e.Action == "deactivate" && e.EntityType == "Faculty");
    }

    [Fact]
    public async Task DeactivateAsync_rejects_a_Faculty_that_is_already_inactive()
    {
        var faculty = Faculty.Create(CampusId.New(), "Faculty of Engineering", DateTimeOffset.UtcNow);
        faculty.Deactivate(DateTimeOffset.UtcNow);

        var faculties = new FakeFacultyRepository();
        faculties.Seed(faculty);
        var departments = new FakeDepartmentRepository();

        var service = BuildService(faculties, departments);

        var result = await service.DeactivateAsync(faculty.Id.Value, faculty.Version, _audit);

        Assert.True(result.IsFailure);
        Assert.Equal("faculty.already_inactive", result.Error!.Code);
    }
}
