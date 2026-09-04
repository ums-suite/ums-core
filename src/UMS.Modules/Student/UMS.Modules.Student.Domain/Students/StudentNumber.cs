using System.Text.RegularExpressions;

namespace UMS.Modules.Student.Domain.Students;

/// <summary>
/// requirement-spec.md student §2 Student Number Generation, §9 decision 1: a first-pass
/// placeholder shape <c>{admissionYear}{facultyCode}{sequence}</c> - the BRD does not specify a
/// format, and the final scheme is deferred to Organization's own code-registry conventions
/// (non-blocking per §9). The numeric <paramref name="Sequence"/> component itself is issued by a
/// PostgreSQL <c>SEQUENCE</c> scoped per <c>(admissionYear, facultyCode)</c> (design-decisions.md,
/// "StudentNumber Generation &amp; Uniqueness Mechanism") - this value object only formats/parses
/// the already-issued value, it never computes the sequence itself (that would reintroduce the
/// exact read-then-write race the sequence exists to eliminate).
/// </summary>
public sealed partial record StudentNumber
{
    private const int SequenceWidth = 5;

    private StudentNumber(string value, int admissionYear, string facultyCode, long sequence)
    {
        Value = value;
        AdmissionYear = admissionYear;
        FacultyCode = facultyCode;
        Sequence = sequence;
    }

    public string Value { get; }

    public int AdmissionYear { get; }

    public string FacultyCode { get; }

    public long Sequence { get; }

    /// <summary>Formats an already-issued sequence value into the full <see cref="StudentNumber"/> - never generates the sequence value itself.</summary>
    public static StudentNumber FromIssuedSequence(int admissionYear, string facultyCode, long sequence)
    {
        if (!IsValidFacultyCode(facultyCode))
        {
            throw new ArgumentException("Faculty code must be 1-10 upper-case letters/digits.", nameof(facultyCode));
        }

        var value = $"{admissionYear}{facultyCode}{sequence.ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(SequenceWidth, '0')}";
        return new StudentNumber(value, admissionYear, facultyCode, sequence);
    }

    /// <summary>Faculty-code validation shared by <see cref="FromIssuedSequence"/> and the Application layer's own pre-check before it ever reaches the sequence-issuing infrastructure call.</summary>
    public static bool IsValidFacultyCode(string? facultyCode) =>
        !string.IsNullOrWhiteSpace(facultyCode) && FacultyCodePattern().IsMatch(facultyCode);

    public override string ToString() => Value;

    [GeneratedRegex("^[A-Z0-9]{1,10}$")]
    private static partial Regex FacultyCodePattern();
}
