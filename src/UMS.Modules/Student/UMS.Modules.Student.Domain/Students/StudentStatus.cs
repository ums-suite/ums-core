namespace UMS.Modules.Student.Domain.Students;

/// <summary>
/// requirement-spec.md student §2 Status Lifecycle: <c>Enrolled -&gt; Active -&gt;
/// Graduated/Suspended/Transferred</c>, with <c>Suspended -&gt; Active</c> the sole reinstatement
/// exception (§4: "no skipped or backward transition outside the two explicitly modeled
/// exceptions"). Legal-transition enforcement lives on <see cref="Student"/> itself
/// (<see cref="Student.ChangeStatus"/>), never here or in an external service.
/// </summary>
public enum StudentStatus
{
    Enrolled,
    Active,
    Graduated,
    Suspended,
    Transferred,
}
