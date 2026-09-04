using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using UMS.Modules.Student.Application.Abstractions;
using UMS.Modules.Student.Domain.Students;

namespace UMS.Modules.Student.Infrastructure.Persistence;

/// <summary>
/// design-decisions.md, "StudentNumber Generation &amp; Uniqueness Mechanism": a PostgreSQL
/// <c>SEQUENCE</c> scoped per <c>(admissionYear, facultyCode)</c>, created idempotently
/// (<c>CREATE SEQUENCE IF NOT EXISTS</c>) on first use and then read via <c>nextval</c> - the
/// canonical mechanism for handing out a monotonically-increasing value to concurrent callers with
/// NO read-then-write race window, unlike an application-level "read max, compute next" pattern
/// (edge-cases.md, "StudentNumber generation collides under concurrent creation load"). Runs as a
/// standalone statement outside the caller's later Student-insert transaction on purpose - a
/// PostgreSQL sequence is not transactional (values are never "returned" on rollback), which is the
/// explicitly accepted trade-off design-decisions.md names ("small numeric gaps ... accepted
/// because ... only global uniqueness and immutability are the stated hard requirements").
/// </summary>
internal sealed class StudentNumberSequence(StudentDbContext context) : IStudentNumberSequence
{
    public async Task<long> NextAsync(int admissionYear, string facultyCode, CancellationToken cancellationToken = default)
    {
        if (!StudentNumber.IsValidFacultyCode(facultyCode))
        {
            throw new ArgumentException("Faculty code must be 1-10 upper-case letters/digits.", nameof(facultyCode));
        }

        // facultyCode is already validated above against ^[A-Z0-9]{1,10}$ and admissionYear is an
        // int - neither can contain a quote/injection-relevant character, but the sequence name is
        // still wrapped in a double-quoted Postgres identifier as defense-in-depth, never
        // interpolated as a bare unquoted identifier.
        var sequenceName = $"seq_student_number_{admissionYear}_{facultyCode}";

        var connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await using var createCommand = connection.CreateCommand();
            createCommand.CommandText = $"CREATE SEQUENCE IF NOT EXISTS student.\"{sequenceName}\" AS BIGINT START WITH 1 INCREMENT BY 1;";
            await createCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.DuplicateTable)
        {
            // Genuinely observed under real concurrent load (not merely theoretical): PostgreSQL's
            // own catalog-level race window for "CREATE SEQUENCE IF NOT EXISTS" against the exact
            // same not-yet-existing name, hit by two callers at once, can still surface as a raw
            // unique-violation (23505) OR a duplicate-relation error (42P07) on the system catalog
            // instead of silently no-opping, depending on exactly which internal check the two
            // racing DDL statements collide on - the loser here simply proceeds to `nextval`
            // against the sequence the winner just created, regardless of which of the two it saw.
        }

        await using var nextValueCommand = connection.CreateCommand();
        nextValueCommand.CommandText = $"SELECT nextval('student.\"{sequenceName}\"');";
        var result = await nextValueCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
    }
}
