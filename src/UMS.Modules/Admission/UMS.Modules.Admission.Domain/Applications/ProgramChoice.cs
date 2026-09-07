using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Admission.Domain.Applications;

/// <summary>docs/ddd/ubiquitous-language.md: "an ordered preference for a Program within an Application" - nested inside <see cref="Application"/>, never a separate row Applications are fragmented across (requirement-spec.md §9 decision 1).</summary>
public sealed record ProgramChoice
{
    private ProgramChoice(Guid programId, int rank)
    {
        ProgramId = programId;
        Rank = rank;
    }

    // EF Core materialization only (PropertyAccessMode.Field) - never called from application code.
    private ProgramChoice()
    {
    }

    public Guid ProgramId { get; }

    /// <summary>1-based preference order - lower is more preferred.</summary>
    public int Rank { get; }

    public static Result<ProgramChoice> Create(Guid programId, int rank)
    {
        if (programId == Guid.Empty)
        {
            return Error.Validation("program_choice.program_id_required", "A ProgramChoice's programId is required.");
        }

        return rank < 1
            ? Error.Validation("program_choice.rank_invalid", "A ProgramChoice's rank must be at least 1.")
            : new ProgramChoice(programId, rank);
    }
}
