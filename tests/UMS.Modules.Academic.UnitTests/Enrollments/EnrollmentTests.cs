using UMS.Modules.Academic.Domain.Common;
using UMS.Modules.Academic.Domain.Enrollments;
using UMS.Modules.Academic.Domain.Events;

namespace UMS.Modules.Academic.UnitTests.Enrollments;

/// <summary>requirement-spec.md §4: Enrollment status transitions (ACD-6/ACD-7/ACD-8).</summary>
public sealed class EnrollmentTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly CreditHours ThreeCredits = CreditHours.Create(3).Value;

    private static Enrollment CreateEnrollment(bool requiresAdvisorApproval = false) =>
        Enrollment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ThreeCredits, requiresAdvisorApproval, prerequisiteOverrideReason: null, Now);

    [Fact]
    public void Create_without_advisor_approval_required_starts_Active()
    {
        var enrollment = CreateEnrollment(requiresAdvisorApproval: false);

        Assert.Equal(EnrollmentStatus.Active, enrollment.Status);
        Assert.NotNull(enrollment.ApprovedAt);
    }

    [Fact]
    public void Create_with_advisor_approval_required_starts_Pending()
    {
        var enrollment = CreateEnrollment(requiresAdvisorApproval: true);

        Assert.Equal(EnrollmentStatus.Pending, enrollment.Status);
        Assert.Null(enrollment.ApprovedAt);
    }

    [Fact]
    public void Create_raises_EnrollmentCreated_regardless_of_the_resulting_status()
    {
        var enrollment = CreateEnrollment(requiresAdvisorApproval: true);

        var domainEvent = Assert.Single(enrollment.DomainEvents);
        Assert.IsType<EnrollmentCreated>(domainEvent);
    }

    [Fact]
    public void Approve_moves_a_Pending_Enrollment_to_Active()
    {
        var enrollment = CreateEnrollment(requiresAdvisorApproval: true);

        enrollment.Approve(Now);

        Assert.Equal(EnrollmentStatus.Active, enrollment.Status);
        Assert.NotNull(enrollment.ApprovedAt);
    }

    [Fact]
    public void Approve_an_already_Active_Enrollment_throws()
    {
        var enrollment = CreateEnrollment(requiresAdvisorApproval: false);

        Assert.Throws<InvalidOperationException>(() => enrollment.Approve(Now));
    }

    [Fact]
    public void Drop_an_Active_Enrollment_moves_to_Dropped_and_raises_EnrollmentDropped()
    {
        var enrollment = CreateEnrollment(requiresAdvisorApproval: false);
        enrollment.ClearDomainEvents();

        enrollment.Drop("Schedule conflict", Now);

        Assert.Equal(EnrollmentStatus.Dropped, enrollment.Status);
        Assert.Equal("Schedule conflict", enrollment.DropReason);
        var domainEvent = Assert.Single(enrollment.DomainEvents);
        Assert.IsType<EnrollmentDropped>(domainEvent);
    }

    [Fact]
    public void Drop_a_Pending_Enrollment_throws()
    {
        var enrollment = CreateEnrollment(requiresAdvisorApproval: true);

        Assert.Throws<InvalidOperationException>(() => enrollment.Drop(null, Now));
    }

    [Fact]
    public void Drop_an_already_Dropped_Enrollment_throws()
    {
        var enrollment = CreateEnrollment(requiresAdvisorApproval: false);
        enrollment.Drop(null, Now);

        Assert.Throws<InvalidOperationException>(() => enrollment.Drop(null, Now));
    }

    [Fact]
    public void SubmitGrade_attaches_a_Grade_and_raises_GradeSubmitted()
    {
        var enrollment = CreateEnrollment(requiresAdvisorApproval: false);
        enrollment.ClearDomainEvents();
        var assessmentId = Guid.NewGuid();

        enrollment.SubmitGrade([(assessmentId, 85m)], UMS.Shared.Domain.PercentageOrGpa.CreatePercentage(85m).Value, "A", Guid.NewGuid(), Now);

        Assert.NotNull(enrollment.Grade);
        Assert.Equal("A", enrollment.Grade.LetterGrade);
        Assert.Contains(enrollment.DomainEvents, e => e is GradeSubmitted);
    }
}
