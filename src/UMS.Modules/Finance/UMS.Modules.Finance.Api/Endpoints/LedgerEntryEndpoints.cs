using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Finance.Application.Abstractions;
using UMS.Modules.Finance.Application.Ledger;
using UMS.Modules.Finance.Application.Permissions;
using UMS.Modules.Finance.Domain.Ledger;
using UMS.Shared.Authorization;

namespace UMS.Modules.Finance.Api.Endpoints;

/// <summary>FIN-13: requirement-spec.md §6 - "GET /api/v1/finance/ledger-entries | Inbound API | Accountant/Reporting read only, append-only backing store."</summary>
internal static class LedgerEntryEndpoints
{
    public static void MapLedgerEntryEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/ledger-entries", async (
            string? referenceType,
            Guid? referenceId,
            string? entryType,
            DateTimeOffset? occurredFrom,
            DateTimeOffset? occurredTo,
            int? skip,
            int? take,
            LedgerEntryQueryService service,
            CancellationToken cancellationToken) =>
        {
            LedgerEntryType? parsedEntryType = null;
            if (!string.IsNullOrWhiteSpace(entryType))
            {
                if (!Enum.TryParse<LedgerEntryType>(entryType, ignoreCase: true, out var value))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { [nameof(entryType)] = [$"'{entryType}' is not a recognized LedgerEntryType."] });
                }

                parsedEntryType = value;
            }

            var filter = new LedgerEntryFilter(referenceType, referenceId, parsedEntryType, occurredFrom, occurredTo);
            var page = await service.ListAsync(filter, skip ?? 0, take ?? 50, cancellationToken).ConfigureAwait(false);
            return Results.Ok(page);
        }).RequirePermission(FinancePermissions.LedgerRead);
    }
}
