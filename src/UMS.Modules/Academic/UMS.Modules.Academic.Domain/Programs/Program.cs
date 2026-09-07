using UMS.Modules.Academic.Domain.Common;

namespace UMS.Modules.Academic.Domain.Programs;

/// <summary>
/// ACD-1 (bundled foundation): a degree offering owned by a Department (docs/ddd/ubiquitous-
/// language.md, "Owned by Academic"). Not itself a separately numbered ticket in tickets.md -
/// bundled into ACD-1's own scope because `Curriculum` (ACD-1's actual ticket) cannot attach to a
/// Program that doesn't yet exist, exactly the same "necessary supporting capability bundled into
/// the ticket it unblocks" judgment call this build documents for `AcademicSession`/`Semester`
/// under ACD-3.
///
/// <para>
/// <see cref="MaxCreditsPerSemester"/> is the credit-limit gate's own configured ceiling
/// (requirement-spec.md §2/§4 credit-limit-gate invariant) - resolved by ACD-6's Enrollment
/// transaction directly off this aggregate, never re-derived per call.
/// </para>
/// </summary>
public sealed class Program : AggregateRoot<ProgramId>
{
    private Program()
    {
    }

    private Program(ProgramId id, Guid departmentId, string code, string name, int maxCreditsPerSemester, bool requiresAdvisorApproval, DateTimeOffset now)
    {
        Id = id;
        DepartmentId = departmentId;
        Code = code;
        Name = name;
        MaxCreditsPerSemester = maxCreditsPerSemester;
        RequiresAdvisorApproval = requiresAdvisorApproval;
        CreatedAt = now;
    }

    public Guid DepartmentId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public int MaxCreditsPerSemester { get; private set; }

    /// <summary>requirement-spec.md §2: "configurable per Program as to whether this gate is required" - ACD-8's own Advisor-approval-gate switch.</summary>
    public bool RequiresAdvisorApproval { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Program Create(Guid departmentId, string code, string name, int maxCreditsPerSemester, bool requiresAdvisorApproval, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Program code is required.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Program name is required.", nameof(name));
        }

        if (maxCreditsPerSemester < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCreditsPerSemester), "MaxCreditsPerSemester must be at least 1.");
        }

        return new Program(ProgramId.New(), departmentId, code.Trim(), name.Trim(), maxCreditsPerSemester, requiresAdvisorApproval, now);
    }
}
