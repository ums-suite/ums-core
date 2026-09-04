using UMS.Modules.Organization.Domain.Common;
using UMS.Modules.Organization.Domain.Departments;
using UMS.Modules.Organization.Domain.Events;
using UMS.Modules.Organization.Domain.Faculties;

namespace UMS.Modules.Organization.UnitTests.Departments;

public class DepartmentTests
{
    private static readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    [Fact]
    public void Create_raises_DepartmentCreated()
    {
        var facultyId = FacultyId.New();

        var department = Department.Create(facultyId, "Computer Science", _now);

        var raised = Assert.Single(department.DomainEvents);
        var created = Assert.IsType<DepartmentCreated>(raised);
        Assert.Equal(department.Id.Value, created.DepartmentId);
        Assert.Equal(facultyId.Value, created.FacultyId);
    }

    [Fact]
    public void Two_departments_with_the_same_name_under_different_faculties_are_independent_domain_objects()
    {
        // requirement-spec.md organization §4: "uniqueness is scoped to the immediate parent, not
        // global" - the domain layer itself places no restriction on this; the DB-level composite
        // unique constraint (edge-cases.md) is what actually enforces parent-scoped uniqueness,
        // exercised in the integration test suite instead.
        var first = Department.Create(FacultyId.New(), "Mathematics", _now);
        var second = Department.Create(FacultyId.New(), "Mathematics", _now);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(first.Name, second.Name);
    }

    [Fact]
    public void Deactivate_raises_DepartmentDeactivated_and_rejects_a_second_call()
    {
        var department = Department.Create(FacultyId.New(), "Computer Science", _now);
        department.ClearDomainEvents();

        department.Deactivate(_now);

        Assert.Equal(NodeStatus.Inactive, department.Status);
        Assert.IsType<DepartmentDeactivated>(Assert.Single(department.DomainEvents));
        Assert.Throws<InvalidOperationException>(() => department.Deactivate(_now));
    }

    [Fact]
    public void Rename_raises_OrganizationNodeRenamed_only_when_the_name_changes()
    {
        var department = Department.Create(FacultyId.New(), "Computer Science", _now);
        department.ClearDomainEvents();

        Assert.False(department.Rename("Computer Science", _now));
        Assert.True(department.Rename("Computer Science and Engineering", _now));
        Assert.IsType<OrganizationNodeRenamed>(Assert.Single(department.DomainEvents));
    }
}
