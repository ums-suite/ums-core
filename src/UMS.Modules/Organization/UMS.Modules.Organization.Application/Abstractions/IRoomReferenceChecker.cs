namespace UMS.Modules.Organization.Application.Abstractions;

/// <summary>
/// edge-cases.md "Hard-delete cross-module reference check races a new reference being created":
/// resolves whether any other module (Hostel's bed-allocation overflow use, Academic's exam-room
/// scheduling - neither exists yet, Flows #19/#12) still holds a live reference to a Room by id,
/// via each dependent module's own read interface (ADR-0002). Until those modules land, the one
/// Infrastructure registration against this interface is an explicit stub seam
/// (<c>StubRoomReferenceChecker</c>) - see that class's own remarks, mirroring
/// <c>IOrganizationNodeExistenceChecker</c>'s own former-stub pattern in Identity.
/// </summary>
public interface IRoomReferenceChecker
{
    /// <summary>True if any dependent module still references <paramref name="roomId"/> - a true result blocks the Room's hard delete (design-decisions.md, "Cross-Module Hard-Delete Reference Check Timing").</summary>
    public Task<bool> HasAnyReferencesAsync(Guid roomId, CancellationToken cancellationToken = default);
}
