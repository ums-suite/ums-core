using Microsoft.Extensions.Logging;
using UMS.Modules.Admission.Application.Abstractions;
using UMS.Modules.Admission.Domain.Applications;
using UMS.Modules.Admission.Domain.Tests;
using UMS.Shared.Documents;
using ApplicationId = UMS.Modules.Admission.Domain.Applications.ApplicationId;

namespace UMS.Modules.Admission.Application.Applications;

/// <summary>
/// ADM-9: admit-card generation once an <see cref="Domain.Applications.Application"/> is Locked -
/// assigns a test slot/roll number via the atomic conditional-decrement mechanism (edge-cases.md
/// "Test-slot capacity race at admit-card generation"), then requests the admit card itself via
/// Documents (ADR-0010), best-effort (a Documents outage never re-opens or fails the already-
/// committed Application, the same posture Student's/Finance's own synchronous Documents calls
/// take).
/// </summary>
public sealed class AdmitCardService(
    IApplicationRepository applications,
    IAdmissionTestRepository tests,
    IDocumentGenerationRequester documentGenerationRequester,
    IUnitOfWork unitOfWork,
    ILogger<AdmitCardService> logger)
{
    private const string AdmitCardDocumentType = "AdmitCard";

    public async Task AssignAndGenerateAsync(Guid applicationId, Guid campaignId, string correlationId, CancellationToken cancellationToken = default)
    {
        var test = await tests.GetByCampaignIdAsync(campaignId, cancellationToken).ConfigureAwait(false);
        if (test is null || test.Slots.Count == 0)
        {
            logger.LogWarning("Admit card: no AdmissionTest/TestSlot configured for Campaign {CampaignId} - Application {ApplicationId} remains Locked without an admit card.", campaignId, applicationId);
            return;
        }

        TestSlot? claimedSlot = null;
        foreach (var slot in test.Slots.OrderBy(s => s.StartAt))
        {
            if (await tests.TryClaimSlotSeatAsync(slot.Id, cancellationToken).ConfigureAwait(false))
            {
                claimedSlot = slot;
                break;
            }
        }

        if (claimedSlot is null)
        {
            // edge-cases.md residual note: every configured slot exhausted - a real operational
            // alert, never a silent drop.
            logger.LogError("Admit card: every TestSlot for AdmissionTest {TestId} (Campaign {CampaignId}) is at capacity - Application {ApplicationId} remains Locked without a completed admit card. Requires Admission Officer capacity-planning action.", test.Id, campaignId, applicationId);
            return;
        }

        var application = await applications.GetByIdAsync(new ApplicationId(applicationId), cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            return;
        }

        var rollNumber = $"{claimedSlot.Id.Value:N}".Substring(0, 8).ToUpperInvariant();
        application.AssignTestSlot(claimedSlot.Id.Value, rollNumber);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var requested = await documentGenerationRequester.RequestAsync(
                new RequestDocumentGenerationCommand(
                    application.ApplicantId,
                    AdmitCardDocumentType,
                    application.Id.Value,
                    new Dictionary<string, string> { ["rollNumber"] = rollNumber, ["applicationNumber"] = application.ApplicationNumber ?? string.Empty },
                    LanguageCode: null,
                    RequestedByUserId: null,
                    correlationId),
                cancellationToken).ConfigureAwait(false);

            if (requested.IsSuccess)
            {
                application.RecordAdmitCard(requested.Value.DocumentId);
                await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            else if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning("Admit card: Documents rejected the generation request for Application {ApplicationId}: {Error}.", applicationId, requested.Error);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Admit card: Documents call failed for Application {ApplicationId} - the slot assignment itself already committed.", applicationId);
        }
    }
}
