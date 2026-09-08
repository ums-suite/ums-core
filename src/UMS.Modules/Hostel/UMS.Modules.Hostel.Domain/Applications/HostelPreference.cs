using UMS.Modules.Hostel.Domain.Hostels;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Hostel.Domain.Applications;

/// <summary>
/// requirement-spec.md §2 step 2: "ranked hostel/room-type preferences." Persisted as an
/// EF <c>OwnsMany</c> owned collection keyed by a shadow <c>Ordinal</c> property, not
/// <c>(ApplicationId, HostelId)</c> - the exact EF Core 10 owned-collection insert-vs-update bug
/// class this repo's own <c>ProgramChoice</c>/<c>InvoiceItem</c> precedent already works around
/// (see <c>HostelApplicationConfiguration</c>'s own remarks).
/// </summary>
public sealed record HostelPreference(Guid HostelId, RoomType PreferredRoomType, int Rank)
{
    public static Result<HostelPreference> Create(Guid hostelId, RoomType preferredRoomType, int rank)
    {
        if (hostelId == Guid.Empty)
        {
            return Error.Validation("hostel_preference.hostel_id_required", "A HostelPreference requires a non-empty hostelId.");
        }

        if (rank <= 0)
        {
            return Error.Validation("hostel_preference.rank_must_be_positive", "A HostelPreference's rank must be a positive number.");
        }

        return new HostelPreference(hostelId, preferredRoomType, rank);
    }
}
