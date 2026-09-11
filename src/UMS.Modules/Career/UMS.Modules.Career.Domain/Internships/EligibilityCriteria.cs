namespace UMS.Modules.Career.Domain.Internships;

/// <summary>
/// requirement-spec.md §2.2, §3 (module-local term, pending glossary merge): an optional program/
/// department restriction, an optional minimum CGPA, an optional minimum year-of-study. Any criterion
/// left unset means "no restriction on that dimension."
///
/// <para>
/// design-decisions.md "Self-Declared CGPA/Year-of-Study Eligibility": <see cref="ProgramIds"/> is the
/// ONLY dimension hard-enforced automatically (against the applying Student's real `Program`, resolved
/// via `UMS.Shared.Student.IStudentStatusChecker` - never a direct `Academic` dependency).
/// <see cref="MinCgpa"/>/<see cref="MinYearOfStudy"/> are compared only against what the Student
/// SELF-DECLARES on the `CareerApplication` itself (never against any external, live-verified source)
/// - see <see cref="IsSatisfiedByProgram"/> vs. <see cref="IsSatisfiedByDeclaredValues"/>.
/// </para>
/// </summary>
public sealed record EligibilityCriteria(IReadOnlyCollection<Guid> ProgramIds, decimal? MinCgpa, int? MinYearOfStudy)
{
    public static EligibilityCriteria None { get; } = new([], null, null);

    public bool IsSatisfiedByProgram(Guid studentProgramId) => ProgramIds.Count == 0 || ProgramIds.Contains(studentProgramId);

    public bool IsSatisfiedByDeclaredValues(decimal? declaredCgpa, int? declaredYearOfStudy)
    {
        if (MinCgpa is { } minCgpa && (declaredCgpa is null || declaredCgpa < minCgpa))
        {
            return false;
        }

        if (MinYearOfStudy is { } minYear && (declaredYearOfStudy is null || declaredYearOfStudy < minYear))
        {
            return false;
        }

        return true;
    }
}
