namespace UMS.Modules.Learning.Domain.LectureMaterials;

public readonly record struct LectureMaterialId(Guid Value)
{
    public static LectureMaterialId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
