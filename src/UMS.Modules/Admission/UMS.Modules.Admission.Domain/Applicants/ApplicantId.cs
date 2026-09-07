namespace UMS.Modules.Admission.Domain.Applicants;

public readonly record struct ApplicantId(Guid Value)
{
    public static ApplicantId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
