namespace UMS.Modules.Audit.Domain.Exports;

/// <summary>The only legal transitions are Pending -&gt; Processing -&gt; (Completed | Failed) - see <see cref="AuditExportRequest"/>.</summary>
public enum ExportStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3,
}
