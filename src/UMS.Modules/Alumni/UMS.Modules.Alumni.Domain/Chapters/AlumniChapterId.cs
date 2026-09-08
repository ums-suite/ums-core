namespace UMS.Modules.Alumni.Domain.Chapters;

public readonly record struct AlumniChapterId(Guid Value)
{
    public static AlumniChapterId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
