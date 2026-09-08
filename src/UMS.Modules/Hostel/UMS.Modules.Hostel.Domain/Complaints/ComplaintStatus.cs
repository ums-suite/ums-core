namespace UMS.Modules.Hostel.Domain.Complaints;

/// <summary>requirement-spec.md §2 Complaints: "Open -&gt; InProgress -&gt; Resolved or Rejected, with a resolution note".</summary>
public enum ComplaintStatus
{
    Open,
    InProgress,
    Resolved,
    Rejected,
}
