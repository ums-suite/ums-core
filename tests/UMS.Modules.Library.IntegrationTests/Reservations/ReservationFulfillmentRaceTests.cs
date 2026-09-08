using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Library.Application.Loans;
using UMS.Modules.Library.Application.Reservations;
using UMS.Modules.Library.Domain.Common;
using UMS.Modules.Library.IntegrationTests.Infrastructure;

namespace UMS.Modules.Library.IntegrationTests.Reservations;

/// <summary>
/// LIB-9: edge-cases.md "Two queued reservations racing to claim a copy that just became available" -
/// two concurrent <see cref="ReservationFulfillmentService.FulfillAsync"/> invocations for the SAME
/// freed copy (simulating at-least-once outbox-relay delivery, ADR-0014) must offer the copy to
/// exactly one queued Reservation, never both (design-decisions.md "Reservation-Queue Fairness and
/// Claim Mechanism").
/// </summary>
[Collection(LibraryApiTestCollectionDefinition.Name)]
public sealed class ReservationFulfillmentRaceTests(LibraryServiceFixture fixture)
{
    [Fact]
    public async Task Two_concurrent_fulfillment_attempts_for_the_same_freed_copy_offer_it_to_exactly_one_reservation()
    {
        var (bookId, bookCopyId) = await LibraryTestData.CreateBookWithSingleCopyAsync(fixture.Services);

        var borrowerHoldingCopy = Guid.NewGuid();
        var queuedBorrowerA = Guid.NewGuid();
        var queuedBorrowerB = Guid.NewGuid();
        LibraryTestData.RegisterActiveStudent(fixture, borrowerHoldingCopy, Guid.NewGuid());
        LibraryTestData.RegisterActiveStudent(fixture, queuedBorrowerA, Guid.NewGuid());
        LibraryTestData.RegisterActiveStudent(fixture, queuedBorrowerB, Guid.NewGuid());

        using (var setupScope = fixture.Services.CreateScope())
        {
            var loanService = setupScope.ServiceProvider.GetRequiredService<LoanService>();
            var issued = await loanService.IssueAsync(bookCopyId, borrowerHoldingCopy, BorrowerType.Student, issuedByUserId: Guid.NewGuid(), correlationId: Guid.NewGuid().ToString());
            Assert.True(issued.IsSuccess);

            var reservationService = setupScope.ServiceProvider.GetRequiredService<ReservationService>();
            var reservationA = await reservationService.CreateAsync(bookId, queuedBorrowerA, BorrowerType.Student);
            var reservationB = await reservationService.CreateAsync(bookId, queuedBorrowerB, BorrowerType.Student);
            Assert.True(reservationA.IsSuccess);
            Assert.True(reservationB.IsSuccess);

            // Free the copy - it is now Available, with a non-empty queue behind it.
            var returned = await loanService.ReturnAsync(issued.Value.Id, actorUserId: Guid.NewGuid(), correlationId: Guid.NewGuid().ToString());
            Assert.True(returned.IsSuccess);
        }

        async Task<bool> FulfillAsync()
        {
            using var scope = fixture.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ReservationFulfillmentService>();
            return await service.FulfillAsync(bookCopyId, correlationId: Guid.NewGuid().ToString());
        }

        var results = await Task.WhenAll(FulfillAsync(), FulfillAsync());

        Assert.Single(results, r => r);
        Assert.Single(results, r => !r);

        using var verifyScope = fixture.Services.CreateScope();
        var reservationsAfter = verifyScope.ServiceProvider.GetRequiredService<ReservationService>();
        var allReservations = (await reservationsAfter.GetByBorrowerAsync(queuedBorrowerA)).Concat(await reservationsAfter.GetByBorrowerAsync(queuedBorrowerB)).ToList();

        // Exactly one Reservation was offered the freed copy - never both, never neither.
        Assert.Single(allReservations, r => r.Status == "Offered");
        Assert.Single(allReservations, r => r.Status == "Queued");
    }
}
