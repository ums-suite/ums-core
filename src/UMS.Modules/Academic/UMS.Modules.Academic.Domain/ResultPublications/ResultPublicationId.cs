namespace UMS.Modules.Academic.Domain.ResultPublications;

public readonly record struct ResultPublicationId(Guid Value)
{
    public static ResultPublicationId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
