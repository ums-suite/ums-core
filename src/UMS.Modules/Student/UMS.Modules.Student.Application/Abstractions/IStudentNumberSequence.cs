namespace UMS.Modules.Student.Application.Abstractions;

/// <summary>
/// design-decisions.md, "StudentNumber Generation &amp; Uniqueness Mechanism": issues the next
/// numeric value from a PostgreSQL <c>SEQUENCE</c> scoped per <c>(admissionYear, facultyCode)</c> -
/// no read-then-write race window, unlike an application-level "read max, compute next" pattern
/// (edge-cases.md, "StudentNumber generation collides under concurrent creation load"). The
/// sequence itself is created idempotently (<c>CREATE SEQUENCE IF NOT EXISTS</c>) on first use per
/// distinct <c>(admissionYear, facultyCode)</c> pair.
/// </summary>
public interface IStudentNumberSequence
{
    public Task<long> NextAsync(int admissionYear, string facultyCode, CancellationToken cancellationToken = default);
}
