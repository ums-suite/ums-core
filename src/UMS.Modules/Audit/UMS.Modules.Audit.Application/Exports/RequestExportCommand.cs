using UMS.Modules.Audit.Application.Entries;
using UMS.Modules.Audit.Domain.Exports;

namespace UMS.Modules.Audit.Application.Exports;

/// <summary>AUD-9: <c>POST /audit/exports</c>' request shape - a filtered view snapshot plus the requested output format.</summary>
public sealed record RequestExportCommand(AuditEntryFilter Filter, ExportFormat Format);
