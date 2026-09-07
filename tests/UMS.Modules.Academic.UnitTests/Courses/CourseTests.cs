using UMS.Modules.Academic.Domain.Common;
using UMS.Modules.Academic.Domain.Courses;

namespace UMS.Modules.Academic.UnitTests.Courses;

public sealed class CourseTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static Course CreateCourse(string code = "CSE101") =>
        Course.Create(code, "Introduction to Programming", CreditHours.Create(3).Value, Now);

    [Fact]
    public void AddPrerequisite_records_the_declared_prerequisite()
    {
        var course = CreateCourse();
        var prerequisite = CourseId.New();

        course.AddPrerequisite(prerequisite);

        Assert.Single(course.Prerequisites);
        Assert.Equal(prerequisite, course.Prerequisites.Single().PrerequisiteCourseId);
    }

    [Fact]
    public void AddPrerequisite_is_idempotent_for_the_same_prerequisite()
    {
        var course = CreateCourse();
        var prerequisite = CourseId.New();

        course.AddPrerequisite(prerequisite);
        course.AddPrerequisite(prerequisite);

        Assert.Single(course.Prerequisites);
    }

    [Fact]
    public void AddPrerequisite_rejects_a_Course_declaring_itself_as_its_own_prerequisite()
    {
        var course = CreateCourse();

        Assert.Throws<InvalidOperationException>(() => course.AddPrerequisite(course.Id));
    }
}
