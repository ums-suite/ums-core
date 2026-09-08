namespace UMS.Modules.Alumni.Domain.Alumni;

/// <summary>requirement-spec.md §2.1/§4: record-level directory visibility - defaults to <see cref="Private"/> on creation (§9 decision), opt-in only.</summary>
public enum ProfileVisibility
{
    Private,
    Public,
}
