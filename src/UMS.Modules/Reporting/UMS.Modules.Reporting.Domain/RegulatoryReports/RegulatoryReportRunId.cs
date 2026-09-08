namespace UMS.Modules.Reporting.Domain.RegulatoryReports;

public readonly record struct RegulatoryReportRunId(Guid Value)
{
    public static RegulatoryReportRunId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
