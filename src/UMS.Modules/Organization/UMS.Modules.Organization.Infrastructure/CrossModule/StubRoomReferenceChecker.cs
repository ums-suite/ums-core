using UMS.Modules.Organization.Application.Abstractions;

namespace UMS.Modules.Organization.Infrastructure.CrossModule;

/// <summary>
/// EXPLICIT SEAM, not a real check: neither Hostel (Flow #19) nor Academic (Flow #12) exists yet,
/// so there is no read interface to call for a Room's downstream references. Every Room is
/// reported as unreferenced until at least one of those modules lands and this registration is
/// replaced with one that actually calls their public query interfaces (edge-cases.md, "Hard-
/// delete cross-module reference check races a new reference being created"). Mirrors
/// <see cref="StubFacultyEmploymentChecker"/>'s own remarks.
/// </summary>
internal sealed class StubRoomReferenceChecker : IRoomReferenceChecker
{
    public Task<bool> HasAnyReferencesAsync(Guid roomId, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
