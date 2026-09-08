using UMS.Modules.Alumni.Domain.Alumni;
using UMS.Modules.Alumni.Domain.Chapters;

namespace UMS.Modules.Alumni.UnitTests.Chapters;

public sealed class AlumniChapterTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Join_twice_by_the_same_Alumnus_is_idempotent()
    {
        var chapter = AlumniChapter.Create("Dhaka Chapter", null, "Dhaka", Now);
        var alumnusId = AlumnusId.New();

        chapter.Join(alumnusId, Now);
        chapter.Join(alumnusId, Now.AddDays(1));

        Assert.Single(chapter.Memberships);
    }

    [Fact]
    public void Leave_removes_the_membership()
    {
        var chapter = AlumniChapter.Create("Dhaka Chapter", null, "Dhaka", Now);
        var alumnusId = AlumnusId.New();
        chapter.Join(alumnusId, Now);

        chapter.Leave(alumnusId);

        Assert.Empty(chapter.Memberships);
        Assert.False(chapter.HasMember(alumnusId));
    }
}
