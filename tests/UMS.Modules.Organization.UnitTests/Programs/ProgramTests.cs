using UMS.Modules.Organization.Domain.Common;
using UMS.Modules.Organization.Domain.Departments;
using UMS.Modules.Organization.Domain.Events;
using OrgProgram = UMS.Modules.Organization.Domain.Programs.Program;

namespace UMS.Modules.Organization.UnitTests.Programs;

public class ProgramTests
{
    private static readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    [Fact]
    public void Create_raises_ProgramCreated()
    {
        var departmentId = DepartmentId.New();

        var program = OrgProgram.Create(departmentId, "BSc in CSE", _now);

        var raised = Assert.Single(program.DomainEvents);
        var created = Assert.IsType<ProgramCreated>(raised);
        Assert.Equal(program.Id.Value, created.ProgramId);
        Assert.Equal(departmentId.Value, created.DepartmentId);
    }

    [Fact]
    public void Create_has_no_dependency_on_any_Designation_or_staffing_state()
    {
        // edge-cases.md §8: "A Program created before its Department is fully configured (e.g.,
        // missing Designation setup) ... Legal" - the factory takes only a DepartmentId and name.
        var program = OrgProgram.Create(DepartmentId.New(), "BSc in CSE", _now);

        Assert.Equal(NodeStatus.Active, program.Status);
    }

    [Fact]
    public void Deactivate_raises_ProgramDeactivated_and_rejects_a_second_call()
    {
        var program = OrgProgram.Create(DepartmentId.New(), "BSc in CSE", _now);
        program.ClearDomainEvents();

        program.Deactivate(_now);

        Assert.Equal(NodeStatus.Inactive, program.Status);
        Assert.IsType<ProgramDeactivated>(Assert.Single(program.DomainEvents));
        Assert.Throws<InvalidOperationException>(() => program.Deactivate(_now));
    }
}
