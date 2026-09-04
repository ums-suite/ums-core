namespace UMS.Modules.Audit.Infrastructure.Partitioning;

/// <summary>
/// Pure, DB-free naming/range computation for a monthly partition (AUD-3; requirement-spec.md
/// audit §5/§9.1) - split out from <see cref="AuditPartitionMaintenanceService"/> purely so the
/// naming/boundary math is unit-testable without a real Postgres instance.
/// </summary>
public static class PartitionNaming
{
    public const string ParentTable = "audit.audit_log_entries";

    /// <summary>The month-aligned <c>[start, endExclusive)</c> partition bounds and table name for the calendar month containing <paramref name="pointInTime"/>.</summary>
    public static (string Name, DateTimeOffset RangeStart, DateTimeOffset RangeEndExclusive) ForMonthContaining(DateTimeOffset pointInTime)
    {
        var monthStart = new DateTimeOffset(pointInTime.Year, pointInTime.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var monthEnd = monthStart.AddMonths(1);
        return ($"audit_log_entries_y{monthStart:yyyy}m{monthStart:MM}", monthStart, monthEnd);
    }

    /// <summary>Every monthly partition needed to cover <paramref name="monthsBehind"/> months before through <paramref name="monthsAhead"/> months after the month containing <paramref name="asOf"/>, inclusive.</summary>
    public static IReadOnlyList<(string Name, DateTimeOffset RangeStart, DateTimeOffset RangeEndExclusive)> RequiredPartitions(
        DateTimeOffset asOf,
        int monthsBehind,
        int monthsAhead)
    {
        var currentMonth = ForMonthContaining(asOf);
        var results = new List<(string, DateTimeOffset, DateTimeOffset)>();

        for (var offset = -monthsBehind; offset <= monthsAhead; offset++)
        {
            var monthStart = currentMonth.RangeStart.AddMonths(offset);
            results.Add(ForMonthContaining(monthStart));
        }

        return results;
    }
}
