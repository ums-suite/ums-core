using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Library.Application.Loans;
using UMS.Modules.Library.Application.Reservations;
using UMS.Modules.Library.Domain.Common;
using UMS.Modules.Library.IntegrationTests.Infrastructure;

namespace UMS.Modules.Library.IntegrationTests.Reservations;

/// <summary>LIB-4/LIB-7: requirement-spec.md §4 invariants - a Reservation may only be created when zero copies are Available, and Renewal is rejected while a Reservation queue is non-empty.</summary>
[Collection(LibraryApiTestCollectionDefinition.Name)]
public sealed class ReservationQueueingTests(LibraryServiceFixture fixture)
{
    [Fact]
    public async Task Creating_a_Reservation_while_a_copy_is_still_Available_is_rejected()
    {
        var bookId = await LibraryTestData.CreateBookAsync(fixture.Services);
        await LibraryTestData.CreateBookCopyAsync(fixture.Services, bookId);
        var studentId = Guid.NewGuid();
        LibraryTestData.RegisterActiveStudent(fixture, studentId, Guid.NewGuid());

        using var scope = fixture.Services.CreateScope();
        var reservationService = scope.ServiceProvider.GetRequiredService<ReservationService>();
        var result = await reservationService.CreateAsync(bookId, studentId, BorrowerType.Student);

        Assert.True(result.IsFailure);
        Assert.Equal("reservation.copy_available", result.Error!.Code);
    }

    [Fact]
    public async Task A_Loan_cannot_be_renewed_while_its_Books_reservation_queue_is_non_empty()
    {
        var (bookId, bookCopyId) = await LibraryTestData.CreateBookWithSingleCopyAsync(fixture.Services);
        var holderId = Guid.NewGuid();
        var queuedId = Guid.NewGuid();
        LibraryTestData.RegisterActiveStudent(fixture, holderId, Guid.NewGuid());
        LibraryTestData.RegisterActiveStudent(fixture, queuedId, Guid.NewGuid());

        using var scope = fixture.Services.CreateScope();
        var loanService = scope.ServiceProvider.GetRequiredService<LoanService>();
        var issued = await loanService.IssueAsync(bookCopyId, holderId, BorrowerType.Student, issuedByUserId: Guid.NewGuid(), correlationId: Guid.NewGuid().ToString());
        Assert.True(issued.IsSuccess);

        var reservationService = scope.ServiceProvider.GetRequiredService<ReservationService>();
        var reserved = await reservationService.CreateAsync(bookId, queuedId, BorrowerType.Student);
        Assert.True(reserved.IsSuccess);

        var renewAttempt = await loanService.RenewAsync(issued.Value.Id, actorUserId: Guid.NewGuid(), correlationId: Guid.NewGuid().ToString());

        Assert.True(renewAttempt.IsFailure);
        Assert.Equal("loan.reservation_queue_blocks_renewal", renewAttempt.Error!.Code);
    }

    [Fact]
    public async Task A_borrower_cannot_double_reserve_the_same_Book()
    {
        var (bookId, bookCopyId) = await LibraryTestData.CreateBookWithSingleCopyAsync(fixture.Services);
        var holderId = Guid.NewGuid();
        var queuedId = Guid.NewGuid();
        LibraryTestData.RegisterActiveStudent(fixture, holderId, Guid.NewGuid());
        LibraryTestData.RegisterActiveStudent(fixture, queuedId, Guid.NewGuid());

        using var scope = fixture.Services.CreateScope();
        var loanService = scope.ServiceProvider.GetRequiredService<LoanService>();
        await loanService.IssueAsync(bookCopyId, holderId, BorrowerType.Student, issuedByUserId: Guid.NewGuid(), correlationId: Guid.NewGuid().ToString());

        var reservationService = scope.ServiceProvider.GetRequiredService<ReservationService>();
        var first = await reservationService.CreateAsync(bookId, queuedId, BorrowerType.Student);
        Assert.True(first.IsSuccess);

        var second = await reservationService.CreateAsync(bookId, queuedId, BorrowerType.Student);
        Assert.True(second.IsFailure);
        Assert.Equal("reservation.already_queued", second.Error!.Code);
    }
}
