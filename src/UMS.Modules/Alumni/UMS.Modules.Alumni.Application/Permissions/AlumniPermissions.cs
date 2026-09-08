namespace UMS.Modules.Alumni.Application.Permissions;

/// <summary>
/// requirement-spec.md §7: permission strings namespaced <c>alumni.*</c>, each validated against
/// Identity's own catalog validator (<c>^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*){2,}$</c> - at least 3
/// dot-separated segments, each starting with a letter, no underscores). Every string below was
/// checked against that exact regex before being committed to - <c>alumni.directory.read.private</c>
/// is a 4-segment string, which the regex permits (it requires AT LEAST 3 segments, no upper cap).
///
/// <para>
/// Self-service actions (own profile read/write, directory browse, job apply, donation initiation,
/// mentorship opt-in, event RSVP, accepting a proposed match) are deliberately NOT gated by a
/// dedicated Permission here - they require only a live session (mirrors Research's own Grant
/// activate/close split: an ownership check in the Api layer, not a Permission grant, is what scopes
/// a self-service action to the caller's own record). Only moderator/coordinator/Admin-only actions
/// and the one privacy-sensitive bypass get a Permission string.
/// </para>
/// </summary>
public static class AlumniPermissions
{
    public const string ChapterManage = "alumni.chapter.manage";

    public const string JobModerate = "alumni.job.moderate";

    /// <summary>requirement-spec.md §2.2/§7: Admin/support bypass of directory-visibility filtering - itself an audited access (§5).</summary>
    public const string DirectoryReadPrivate = "alumni.directory.read.private";

    public const string MentorshipCoordinate = "alumni.mentorship.coordinate";

    /// <summary>Internal reconciliation/support view of an anonymous Donation's real donor identity (requirement-spec.md §4 "Anonymity is display-only").</summary>
    public const string DonationReconcile = "alumni.donation.reconcile";
}
