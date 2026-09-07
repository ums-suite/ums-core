namespace UMS.Modules.Learning.Domain.LectureMaterials;

public readonly record struct LectureMaterialVersionId(Guid Value)
{
    public static LectureMaterialVersionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
