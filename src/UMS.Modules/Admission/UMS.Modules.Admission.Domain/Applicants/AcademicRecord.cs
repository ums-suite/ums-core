using UMS.Shared.Domain;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Admission.Domain.Applicants;

/// <summary>One prior qualification an <see cref="Applicant"/> declares on their own profile (requirement-spec.md §2: "maintains a profile (personal/academic-history fields) independent of any specific Application"), checked against <see cref="Campaigns.EligibilityRule"/> at submit/merit-list time.</summary>
public sealed record AcademicRecord
{
    private AcademicRecord(string board, string examName, int passingYear, PercentageOrGpa score)
    {
        Board = board;
        ExamName = examName;
        PassingYear = passingYear;
        Score = score;
    }

    // EF Core materialization only (PropertyAccessMode.Field) - never called from application code.
    private AcademicRecord()
    {
    }

    public string Board { get; } = string.Empty;

    public string ExamName { get; } = string.Empty;

    public int PassingYear { get; }

    public PercentageOrGpa Score { get; } = null!;

    public static Result<AcademicRecord> Create(string board, string examName, int passingYear, PercentageOrGpa score)
    {
        if (string.IsNullOrWhiteSpace(board))
        {
            return Error.Validation("academic_record.board_required", "An AcademicRecord's board is required.");
        }

        if (string.IsNullOrWhiteSpace(examName))
        {
            return Error.Validation("academic_record.exam_name_required", "An AcademicRecord's examName is required.");
        }

        if (passingYear < 1950 || passingYear > DateTime.UtcNow.Year)
        {
            return Error.Validation("academic_record.passing_year_invalid", "An AcademicRecord's passingYear is out of a plausible range.");
        }

        ArgumentNullException.ThrowIfNull(score);
        return new AcademicRecord(board.Trim(), examName.Trim(), passingYear, score);
    }
}
