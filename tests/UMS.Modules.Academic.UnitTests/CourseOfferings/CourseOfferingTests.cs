using UMS.Modules.Academic.Domain.CourseOfferings;
using UMS.Modules.Academic.Domain.Events;

namespace UMS.Modules.Academic.UnitTests.CourseOfferings;

public sealed class CourseOfferingTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static CourseOffering CreateOffering(int capacity = 5) =>
        CourseOffering.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), capacity, Now);

    [Fact]
    public void Create_raises_CourseOfferingPublished()
    {
        var offering = CreateOffering();

        var domainEvent = Assert.Single(offering.DomainEvents);
        Assert.IsType<CourseOfferingPublished>(domainEvent);
        Assert.Equal(0, offering.EnrolledCount);
        Assert.True(offering.HasAvailableSeats);
    }

    [Fact]
    public void Create_rejects_a_non_positive_capacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CourseOffering.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0, Now));
    }

    [Fact]
    public void AssignInstructor_raises_InstructorAssigned_with_the_given_FacultyMemberId()
    {
        var offering = CreateOffering();
        offering.ClearDomainEvents();
        var facultyMemberId = Guid.NewGuid();

        offering.AssignInstructor(facultyMemberId, Now);

        Assert.Equal(facultyMemberId, offering.InstructorFacultyMemberId);
        var domainEvent = Assert.Single(offering.DomainEvents);
        var assigned = Assert.IsType<InstructorAssigned>(domainEvent);
        Assert.Equal(facultyMemberId, assigned.FacultyMemberId);
        Assert.Equal(offering.Id.Value, assigned.CourseOfferingId);
    }

    [Fact]
    public void AssignInstructor_twice_with_the_same_FacultyMemberId_is_a_no_op()
    {
        var offering = CreateOffering();
        var facultyMemberId = Guid.NewGuid();
        offering.AssignInstructor(facultyMemberId, Now);
        offering.ClearDomainEvents();

        offering.AssignInstructor(facultyMemberId, Now);

        Assert.Empty(offering.DomainEvents);
    }

    [Fact]
    public void UnassignInstructor_raises_InstructorUnassigned_and_clears_the_reference()
    {
        var offering = CreateOffering();
        var facultyMemberId = Guid.NewGuid();
        offering.AssignInstructor(facultyMemberId, Now);
        offering.ClearDomainEvents();

        offering.UnassignInstructor(Now);

        Assert.Null(offering.InstructorFacultyMemberId);
        var domainEvent = Assert.Single(offering.DomainEvents);
        var unassigned = Assert.IsType<InstructorUnassigned>(domainEvent);
        Assert.Equal(facultyMemberId, unassigned.FacultyMemberId);
    }

    [Fact]
    public void UnassignInstructor_with_no_current_instructor_is_a_no_op()
    {
        var offering = CreateOffering();
        offering.ClearDomainEvents();

        offering.UnassignInstructor(Now);

        Assert.Empty(offering.DomainEvents);
    }

    [Fact]
    public void AddExam_rejects_assessment_weights_summing_past_100_percent()
    {
        var offering = CreateOffering();
        offering.AddExam("Midterm", [("Section A", 0.6m)]);

        Assert.Throws<InvalidOperationException>(() => offering.AddExam("Final", [("Section A", 0.5m)]));
    }

    [Fact]
    public void AddExam_accepts_assessment_weights_summing_to_exactly_100_percent_across_multiple_exams()
    {
        var offering = CreateOffering();
        offering.AddExam("Midterm", [("Only Component", 0.4m)]);

        offering.AddExam("Final", [("Only Component", 0.6m)]);

        Assert.Equal(2, offering.Exams.Count);
        Assert.Equal(1.0m, offering.Exams.SelectMany(e => e.Assessments).Sum(a => a.Weight));
    }
}
