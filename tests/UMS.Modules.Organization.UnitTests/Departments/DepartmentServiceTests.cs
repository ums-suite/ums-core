using UMS.Modules.Organization.Application.Common;
using UMS.Modules.Organization.Application.Departments;
using UMS.Modules.Organization.Application.Hierarchy;
using UMS.Modules.Organization.Domain.Departments;
using UMS.Modules.Organization.Domain.Faculties;
using UMS.Modules.Organization.UnitTests.TestDoubles;
using UMS.Shared.ErrorHandling.Results;
using OrgProgram = UMS.Modules.Organization.Domain.Programs.Program;

namespace UMS.Modules.Organization.UnitTests.Departments;

/// <summary>edge-cases.md "Deactivating a Department that still has a Faculty member on record" and requirement-spec.md organization §4's generic cascade-block invariant.</summary>
public class DepartmentServiceTests
{
    private static readonly AuditContext _audit = new(Guid.NewGuid(), "127.0.0.1", "test-correlation");

    private static DepartmentService BuildService(
        FakeDepartmentRepository departments,
        FakeProgramRepository programs,
        bool hasActiveFacultyMember = false,
        FakeAuditRecorder? auditRecorder = null)
    {
        var faculties = new FakeFacultyRepository();
        return new DepartmentService(
            departments,
            faculties,
            programs,
            new FakeFacultyEmploymentChecker(hasActiveFacultyMember),
            new FakeUnitOfWork(),
            auditRecorder ?? new FakeAuditRecorder(),
            new FakeOrganizationTreeCache(),
            new HierarchyAncestryResolver(new FakeCampusRepository(), faculties, departments, programs),
            new FakeClock());
    }

    [Fact]
    public async Task DeactivateAsync_rejects_a_Department_with_an_active_Program()
    {
        var department = Department.Create(FacultyId.New(), "Computer Science", DateTimeOffset.UtcNow);
        var program = OrgProgram.Create(department.Id, "BSc in CSE", DateTimeOffset.UtcNow);

        var departments = new FakeDepartmentRepository();
        departments.Seed(department);
        var programs = new FakeProgramRepository();
        programs.Seed(program);

        var service = BuildService(departments, programs);

        var result = await service.DeactivateAsync(department.Id.Value, department.Version, _audit);

        Assert.True(result.IsFailure);
        Assert.Equal("department.has_active_children", result.Error!.Code);
    }

    [Fact]
    public async Task DeactivateAsync_rejects_a_Department_still_reported_as_having_an_active_FacultyMember()
    {
        var department = Department.Create(FacultyId.New(), "Computer Science", DateTimeOffset.UtcNow);

        var departments = new FakeDepartmentRepository();
        departments.Seed(department);

        var service = BuildService(departments, new FakeProgramRepository(), hasActiveFacultyMember: true);

        var result = await service.DeactivateAsync(department.Id.Value, department.Version, _audit);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal("department.has_active_faculty_member", result.Error.Code);
    }

    [Fact]
    public async Task DeactivateAsync_succeeds_when_no_active_children_and_no_active_FacultyMember_remain()
    {
        var department = Department.Create(FacultyId.New(), "Computer Science", DateTimeOffset.UtcNow);
        var auditRecorder = new FakeAuditRecorder();

        var departments = new FakeDepartmentRepository();
        departments.Seed(department);

        var service = BuildService(departments, new FakeProgramRepository(), hasActiveFacultyMember: false, auditRecorder);

        var result = await service.DeactivateAsync(department.Id.Value, department.Version, _audit);

        Assert.True(result.IsSuccess);
        Assert.Equal("Inactive", result.Value.Status);
    }
}
