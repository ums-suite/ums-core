using UMS.Modules.Career.Domain.Drives;

namespace UMS.Modules.Career.UnitTests.Drives;

/// <summary>requirement-spec.md §2.3/§2.6: `Draft -> Scheduled -> RegistrationOpen -> RegistrationClosed -> Completed`, `Cancelled` reachable from any non-terminal state.</summary>
public sealed class CampusRecruitmentDriveTests
{
    private static CampusRecruitmentDrive CreateDrive()
    {
        var now = DateTimeOffset.UtcNow;
        return CampusRecruitmentDrive.Create(Guid.NewGuid(), "Spring Recruitment Drive", Guid.NewGuid(), now.AddDays(10), now.AddDays(1), now.AddDays(5), now);
    }

    [Fact]
    public void Create_throws_when_registration_window_is_inverted()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() => CampusRecruitmentDrive.Create(Guid.NewGuid(), "Drive", Guid.NewGuid(), now.AddDays(10), now.AddDays(5), now.AddDays(1), now));
    }

    [Fact]
    public void Full_happy_path_lifecycle_progresses_through_every_stage()
    {
        var drive = CreateDrive();

        drive.Schedule(DateTimeOffset.UtcNow);
        Assert.Equal(DriveStatus.Scheduled, drive.Status);
        Assert.Single(drive.DomainEvents);

        drive.OpenRegistration();
        Assert.Equal(DriveStatus.RegistrationOpen, drive.Status);
        Assert.True(drive.AcceptsRegistrations());

        drive.CloseRegistration();
        Assert.Equal(DriveStatus.RegistrationClosed, drive.Status);
        Assert.False(drive.AcceptsRegistrations());

        drive.Complete();
        Assert.Equal(DriveStatus.Completed, drive.Status);
    }

    [Fact]
    public void OpenRegistration_throws_unless_the_Drive_is_Scheduled()
    {
        var drive = CreateDrive();
        Assert.Throws<InvalidOperationException>(() => drive.OpenRegistration());
    }

    [Theory]
    [InlineData(DriveStatus.Draft)]
    [InlineData(DriveStatus.Scheduled)]
    [InlineData(DriveStatus.RegistrationOpen)]
    [InlineData(DriveStatus.RegistrationClosed)]
    public void Cancel_succeeds_from_every_non_terminal_status(DriveStatus from)
    {
        var drive = CreateDrive();
        AdvanceTo(drive, from);

        drive.Cancel("Employer withdrew", DateTimeOffset.UtcNow);

        Assert.Equal(DriveStatus.Cancelled, drive.Status);
        Assert.Equal("Employer withdrew", drive.CancellationReason);
    }

    [Fact]
    public void Cancel_throws_once_a_Drive_is_already_terminal()
    {
        var drive = CreateDrive();
        drive.Cancel("first reason", DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => drive.Cancel("second reason", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Cancel_requires_a_non_empty_reason()
    {
        var drive = CreateDrive();
        Assert.Throws<ArgumentException>(() => drive.Cancel("   ", DateTimeOffset.UtcNow));
    }

    private static void AdvanceTo(CampusRecruitmentDrive drive, DriveStatus target)
    {
        if (target == DriveStatus.Draft)
        {
            return;
        }

        drive.Schedule(DateTimeOffset.UtcNow);
        if (target == DriveStatus.Scheduled)
        {
            return;
        }

        drive.OpenRegistration();
        if (target == DriveStatus.RegistrationOpen)
        {
            return;
        }

        drive.CloseRegistration();
    }
}
