using Microsoft.EntityFrameworkCore;
using UMS.Modules.Admission.Application.Abstractions;
using UMS.Modules.Admission.Domain.Tests;

namespace UMS.Modules.Admission.Infrastructure.Persistence.Repositories;

internal sealed class AdmissionTestRepository(AdmissionDbContext context) : IAdmissionTestRepository
{
    public Task<AdmissionTest?> GetByIdAsync(AdmissionTestId id, CancellationToken cancellationToken = default) =>
        context.AdmissionTests
            .Include(t => t.Questions)
            .Include(t => t.Slots)
            .Include(t => t.SelectionRules)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<AdmissionTest?> GetByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
        context.AdmissionTests
            .Include(t => t.Questions)
            .Include(t => t.Slots)
            .Include(t => t.SelectionRules)
            .FirstOrDefaultAsync(t => t.CampaignId == campaignId, cancellationToken);

    public void Add(AdmissionTest test) => context.AdmissionTests.Add(test);

    /// <summary>
    /// edge-cases.md "Test-slot capacity race at admit-card generation": <see cref="TestSlot"/> is
    /// an OwnsMany child (no independent <c>DbSet</c>), so this atomic conditional decrement is raw
    /// parameterized SQL against its own table - the identical mechanism class
    /// <c>CourseOfferingRepository.TryIncrementEnrolledCountAsync</c> uses via
    /// <c>ExecuteUpdateAsync</c>, unavailable here only because EF Core's <c>ExecuteUpdateAsync</c>
    /// LINQ builder requires a `DbSet`-rooted query, which an owned collection reached via
    /// <c>SelectMany</c> is not.
    /// </summary>
    public async Task<bool> TryClaimSlotSeatAsync(TestSlotId slotId, CancellationToken cancellationToken = default)
    {
        var affected = await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE admission.admission_test_slots SET remaining_seats = remaining_seats - 1 WHERE id = {slotId.Value} AND remaining_seats > 0",
            cancellationToken).ConfigureAwait(false);
        return affected == 1;
    }
}
