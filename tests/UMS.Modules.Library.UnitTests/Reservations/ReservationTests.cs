using UMS.Modules.Library.Domain.Common;
using UMS.Modules.Library.Domain.Events;
using UMS.Modules.Library.Domain.Reservations;

namespace UMS.Modules.Library.UnitTests.Reservations;

public sealed class ReservationTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_for_a_Faculty_borrower_gets_higher_priority_than_a_Student()
    {
        var facultyReservation = Reservation.Create(Guid.NewGuid(), Guid.NewGuid(), BorrowerType.Faculty, Now).Value;
        var studentReservation = Reservation.Create(Guid.NewGuid(), Guid.NewGuid(), BorrowerType.Student, Now).Value;

        Assert.True(facultyReservation.Priority < studentReservation.Priority);
    }

    [Fact]
    public void Create_starts_Queued_and_raises_ReservationCreated()
    {
        var reservation = Reservation.Create(Guid.NewGuid(), Guid.NewGuid(), BorrowerType.Student, Now).Value;

        Assert.Equal(ReservationStatus.Queued, reservation.Status);
        Assert.Single(reservation.DomainEvents.OfType<ReservationCreated>());
    }

    [Fact]
    public void Offer_from_Queued_succeeds()
    {
        var reservation = Reservation.Create(Guid.NewGuid(), Guid.NewGuid(), BorrowerType.Student, Now).Value;
        var copyId = Guid.NewGuid();

        var result = reservation.Offer(copyId, Now.AddHours(48), Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(ReservationStatus.Offered, reservation.Status);
        Assert.Equal(copyId, reservation.OfferedCopyId);
    }

    [Fact]
    public void Claim_after_the_window_expired_fails()
    {
        var reservation = Reservation.Create(Guid.NewGuid(), Guid.NewGuid(), BorrowerType.Student, Now).Value;
        reservation.Offer(Guid.NewGuid(), Now.AddHours(1), Now);

        var result = reservation.Claim(Now.AddHours(2));

        Assert.True(result.IsFailure);
        Assert.Equal("reservation.claim_window_expired", result.Error!.Code);
    }

    [Fact]
    public void Claim_within_the_window_succeeds()
    {
        var reservation = Reservation.Create(Guid.NewGuid(), Guid.NewGuid(), BorrowerType.Student, Now).Value;
        reservation.Offer(Guid.NewGuid(), Now.AddHours(48), Now);

        var result = reservation.Claim(Now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(ReservationStatus.Claimed, reservation.Status);
    }

    [Fact]
    public void Expire_from_Offered_raises_ReservationExpired()
    {
        var reservation = Reservation.Create(Guid.NewGuid(), Guid.NewGuid(), BorrowerType.Student, Now).Value;
        var copyId = Guid.NewGuid();
        reservation.Offer(copyId, Now.AddHours(1), Now);

        var result = reservation.Expire(copyId, Now.AddHours(2));

        Assert.True(result.IsSuccess);
        Assert.Equal(ReservationStatus.Expired, reservation.Status);
        Assert.Single(reservation.DomainEvents.OfType<ReservationExpired>());
    }

    [Fact]
    public void Expire_from_Queued_fails_conflict()
    {
        var reservation = Reservation.Create(Guid.NewGuid(), Guid.NewGuid(), BorrowerType.Student, Now).Value;

        var result = reservation.Expire(Guid.NewGuid(), Now);

        Assert.True(result.IsFailure);
        Assert.Equal("reservation.not_offered", result.Error!.Code);
    }
}
