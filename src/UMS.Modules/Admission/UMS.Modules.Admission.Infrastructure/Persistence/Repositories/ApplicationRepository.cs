using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using UMS.Modules.Admission.Application.Abstractions;
using UMS.Modules.Admission.Domain.Applications;
using ApplicationId = UMS.Modules.Admission.Domain.Applications.ApplicationId;

namespace UMS.Modules.Admission.Infrastructure.Persistence.Repositories;

/// <summary>
/// design-decisions.md "Idempotency for Application Submission": <see cref="TryLockAsync"/> is the
/// one state-guarded conditional write shared by the applicant-facing submit endpoint and the
/// Finance-payment-confirmation relay.
///
/// <para>
/// <b>Reload-after-bypass mechanism.</b> <see cref="TryLockAsync"/>/<see cref="TryMarkApplicationFeePaidAsync"/>/
/// <see cref="TryMarkConfirmationFeePaidAsync"/> all write via <c>ExecuteUpdateAsync</c>, bypassing
/// the change tracker entirely (mirrors <c>CourseOfferingRepository</c>'s own seat-limit mechanism).
/// A caller within the SAME scope that already holds this row tracked from an earlier read in this
/// request (e.g. <c>ApplicationService.SubmitAsync</c>'s own initial fetch, immediately followed by
/// <c>TryLockAndAuditAsync</c>'s post-lock re-fetch) would otherwise get back that SAME stale
/// tracked instance from EF's identity map - reflecting none of the bypass write's new column
/// values, exactly the bug class <c>ResultPublicationRepository</c>'s own remarks describe for its
/// identical raw-SQL-transition shape. <see cref="GetByIdAsync"/> closes this by explicitly
/// reloading an already-tracked entry's own scalar values from the database before returning it -
/// a real bug this build's own manual verification pass caught and fixed, not merely a test-harness
/// artifact (the exact same double-fetch-in-one-scope shape is what a real submit request drives).
/// </para>
/// </summary>
internal sealed class ApplicationRepository(AdmissionDbContext context) : IApplicationRepository
{
    public async Task<Domain.Applications.Application?> GetByIdAsync(ApplicationId id, CancellationToken cancellationToken = default)
    {
        var tracked = context.ChangeTracker.Entries<Domain.Applications.Application>().FirstOrDefault(e => e.Entity.Id == id);
        if (tracked is not null)
        {
            await tracked.ReloadAsync(cancellationToken).ConfigureAwait(false);
            return tracked.Entity;
        }

        return await context.Applications
            .Include(a => a.ProgramChoices)
            .Include(a => a.Documents)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<Domain.Applications.Application?> GetByApplicantAndCampaignAsync(Guid applicantId, Guid campaignId, CancellationToken cancellationToken = default) =>
        context.Applications
            .Include(a => a.ProgramChoices)
            .Include(a => a.Documents)
            .FirstOrDefaultAsync(a => a.ApplicantId == applicantId && a.CampaignId == campaignId, cancellationToken);

    public Task<Domain.Applications.Application?> GetByApplicationFeeInvoiceIdAsync(Guid invoiceId, CancellationToken cancellationToken = default) =>
        context.Applications
            .Include(a => a.ProgramChoices)
            .Include(a => a.Documents)
            .FirstOrDefaultAsync(a => a.ApplicationFeeInvoiceId == invoiceId, cancellationToken);

    public Task<Domain.Applications.Application?> GetByConfirmationFeeInvoiceIdAsync(Guid invoiceId, CancellationToken cancellationToken = default) =>
        context.Applications
            .Include(a => a.ProgramChoices)
            .Include(a => a.Documents)
            .FirstOrDefaultAsync(a => a.ConfirmationFeeInvoiceId == invoiceId, cancellationToken);

    public async Task<IReadOnlyList<Domain.Applications.Application>> GetLockedByCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
        await context.Applications
            .Include(a => a.ProgramChoices)
            .Where(a => a.CampaignId == campaignId && a.Status == ApplicationStatus.Locked)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(Domain.Applications.Application application) => context.Applications.Add(application);

    public async Task<bool> TryLockAsync(ApplicationId id, string applicationNumber, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var affected = await context.Applications
            .Where(a => a.Id == id && a.Status == ApplicationStatus.Draft)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(a => a.Status, ApplicationStatus.Locked)
                    .SetProperty(a => a.ApplicationNumber, applicationNumber)
                    .SetProperty(a => a.SubmittedAt, now)
                    .SetProperty(a => a.LockedAt, now),
                cancellationToken)
            .ConfigureAwait(false);
        return affected == 1;
    }

    public async Task<bool> TryMarkApplicationFeePaidAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var affected = await context.Applications
            .Where(a => a.ApplicationFeeInvoiceId == invoiceId && !a.IsApplicationFeePaid)
            .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.IsApplicationFeePaid, true), cancellationToken)
            .ConfigureAwait(false);
        return affected == 1;
    }

    public async Task<bool> TryMarkConfirmationFeePaidAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var affected = await context.Applications
            .Where(a => a.ConfirmationFeeInvoiceId == invoiceId && !a.IsConfirmationFeePaid)
            .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.IsConfirmationFeePaid, true), cancellationToken)
            .ConfigureAwait(false);
        return affected == 1;
    }

    /// <summary>requirement-spec.md §9: a single, static Postgres <c>SEQUENCE</c> (created once by the initial migration, never dynamically) - see <see cref="IApplicationRepository"/>'s own remarks for why this build doesn't need Student's own per-tuple dynamic-sequence mechanism.</summary>
    public async Task<long> NextApplicationNumberSequenceValueAsync(CancellationToken cancellationToken = default)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT nextval('admission.application_number_seq');";
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }
}
