namespace UMS.Modules.Reporting.Domain.RegulatoryReports;

public readonly record struct RegulatoryReportDefinitionId(Guid Value)
{
    public static RegulatoryReportDefinitionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
