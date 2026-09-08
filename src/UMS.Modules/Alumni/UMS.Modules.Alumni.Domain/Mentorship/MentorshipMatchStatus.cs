namespace UMS.Modules.Alumni.Domain.Mentorship;

/// <summary>requirement-spec.md §2.5/§3: <c>Proposed -&gt; Accepted -&gt; Active -&gt; Completed/Ended</c>. This module collapses <c>Accepted</c> into the same instant both sides have accepted (no separate observable delay before it becomes <c>Active</c> - see <see cref="Mentorship.MentorshipMatch"/>'s own remarks).</summary>
public enum MentorshipMatchStatus
{
    Proposed,
    Active,
    Completed,
    Ended,
}
