namespace UMS.Modules.Career.Domain.Drives;

public readonly record struct CampusRecruitmentDriveId(Guid Value)
{
    public static CampusRecruitmentDriveId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
