using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Admission.Domain.Campaigns;

/// <summary>requirement-spec.md §4's seat-quota-bound invariant: "the count of Admitted outcomes a MeritList produces per Program may never exceed that program's campaign-configured seat quota".</summary>
public sealed record SeatQuota
{
    private SeatQuota(Guid programId, int quota)
    {
        ProgramId = programId;
        Quota = quota;
    }

    // EF Core materialization only (PropertyAccessMode.Field) - never called from application code.
    private SeatQuota()
    {
    }

    public Guid ProgramId { get; }

    public int Quota { get; }

    public static Result<SeatQuota> Create(Guid programId, int quota)
    {
        if (programId == Guid.Empty)
        {
            return Error.Validation("seat_quota.program_id_required", "A SeatQuota's programId is required.");
        }

        return quota < 1
            ? Error.Validation("seat_quota.invalid", "A SeatQuota's quota must be at least 1.")
            : new SeatQuota(programId, quota);
    }
}
