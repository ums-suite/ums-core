using Microsoft.Extensions.Logging;
using UMS.Modules.Admission.Application.Abstractions;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Admission.Application.Results;

/// <summary>ADM-19: <c>GET /results/search</c> - served exclusively from Redis post-publish (ADR-0007). A miss is logged as a potential operational bug (edge-cases.md), never silently served from PostgreSQL.</summary>
public sealed class ResultSearchService(IResultCache resultCache, ILogger<ResultSearchService> logger)
{
    public async Task<Result<string>> SearchByApplicationNumberAsync(string applicationNumber, CancellationToken cancellationToken = default)
    {
        var value = await resultCache.GetByApplicationNumberAsync(applicationNumber, cancellationToken).ConfigureAwait(false);
        if (value is not null)
        {
            return value;
        }

        logger.LogWarning("Result search: cache miss for applicationNumber {ApplicationNumber} - if this campaign is Published, this is an operational bug per ADR-0007, not a normal fallback path.", applicationNumber);
        return Error.NotFound("result.not_found", $"No published result exists for application number '{applicationNumber}'.");
    }

    public async Task<Result<string>> SearchByExamRollNumberAsync(Guid examId, string rollNumber, CancellationToken cancellationToken = default)
    {
        var value = await resultCache.GetByExamRollNumberAsync(examId, rollNumber, cancellationToken).ConfigureAwait(false);
        return value is not null
            ? value
            : Error.NotFound("result.not_found", $"No published result exists for exam '{examId}' roll number '{rollNumber}'.");
    }
}
