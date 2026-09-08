namespace UMS.Modules.Research.Domain.Publications;

public readonly record struct PublicationDuplicateCandidateId(Guid Value)
{
    public static PublicationDuplicateCandidateId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
