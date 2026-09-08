namespace UMS.Modules.Research.Domain.Publications;

public readonly record struct PublicationId(Guid Value)
{
    public static PublicationId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
