using UMS.Modules.Career.Domain.ResumeProfiles;

namespace UMS.Modules.Career.UnitTests.ResumeProfiles;

/// <summary>design-decisions.md "ResumeProfile Snapshot-at-Submission Immutability" - the live, editable side; a Delete must remain soft so a past snapshot's foreign reference stays resolvable.</summary>
public sealed class ResumeProfileTests
{
    private static ResumeProfile CreateProfile(bool isDefault = false) =>
        ResumeProfile.Create(Guid.NewGuid(), "General", Guid.NewGuid(), "resume.pdf", isDefault, DateTimeOffset.UtcNow);

    [Fact]
    public void Create_requires_a_non_empty_label()
    {
        Assert.Throws<ArgumentException>(() => ResumeProfile.Create(Guid.NewGuid(), "   ", Guid.NewGuid(), "resume.pdf", false, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Create_raises_a_ResumeProfileUpdated_event()
    {
        var profile = CreateProfile();
        Assert.Single(profile.DomainEvents);
    }

    [Fact]
    public void MarkAsDefault_and_UnmarkAsDefault_toggle_IsDefault()
    {
        var profile = CreateProfile();

        profile.MarkAsDefault();
        Assert.True(profile.IsDefault);

        profile.UnmarkAsDefault();
        Assert.False(profile.IsDefault);
    }

    [Fact]
    public void Update_replaces_the_artifact_only_when_a_new_one_is_supplied()
    {
        var profile = CreateProfile();
        var originalArtifactId = profile.ArtifactId;

        profile.Update("Renamed", null, null, DateTimeOffset.UtcNow);
        Assert.Equal("Renamed", profile.Label);
        Assert.Equal(originalArtifactId, profile.ArtifactId);

        var newArtifactId = Guid.NewGuid();
        profile.Update("Renamed again", newArtifactId, "new-resume.pdf", DateTimeOffset.UtcNow);
        Assert.Equal(newArtifactId, profile.ArtifactId);
        Assert.Equal("new-resume.pdf", profile.FileName);
    }

    [Fact]
    public void Update_throws_once_a_ResumeProfile_is_deleted()
    {
        var profile = CreateProfile();
        profile.Delete();

        Assert.Throws<InvalidOperationException>(() => profile.Update("New label", null, null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Delete_is_a_soft_delete_that_also_clears_the_default_flag()
    {
        var profile = CreateProfile(isDefault: true);

        profile.Delete();

        Assert.True(profile.IsDeleted);
        Assert.False(profile.IsDefault);
    }
}
