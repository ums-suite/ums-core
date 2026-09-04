using UMS.Shared.ErrorHandling.Results;

namespace UMS.Shared.Domain;

/// <summary>
/// A person's given/family name, with an optional Bengali rendering alongside the required
/// English one (ums-conventions.md, Domain Modeling: "PersonName (given/family, correct Bengali +
/// English rendering)"; `ums-requirements.md`'s Bengali-first bilingual requirement).
/// </summary>
public sealed record PersonName
{
    private const int MaxPartLength = 100;

    private PersonName(string givenName, string familyName, string? givenNameBn, string? familyNameBn)
    {
        GivenName = givenName;
        FamilyName = familyName;
        GivenNameBn = givenNameBn;
        FamilyNameBn = familyNameBn;
    }

    public string GivenName { get; }

    public string FamilyName { get; }

    public string? GivenNameBn { get; }

    public string? FamilyNameBn { get; }

    public string DisplayName => $"{GivenName} {FamilyName}";

    public static Result<PersonName> Create(
        string? givenName,
        string? familyName,
        string? givenNameBn = null,
        string? familyNameBn = null)
    {
        if (string.IsNullOrWhiteSpace(givenName))
        {
            return Error.Validation("person_name.given_name_required", "Given name is required.");
        }

        if (string.IsNullOrWhiteSpace(familyName))
        {
            return Error.Validation("person_name.family_name_required", "Family name is required.");
        }

        if (givenName.Trim().Length > MaxPartLength || familyName.Trim().Length > MaxPartLength)
        {
            return Error.Validation("person_name.too_long", $"Each name part must be at most {MaxPartLength} characters.");
        }

        return new PersonName(
            givenName.Trim(),
            familyName.Trim(),
            string.IsNullOrWhiteSpace(givenNameBn) ? null : givenNameBn.Trim(),
            string.IsNullOrWhiteSpace(familyNameBn) ? null : familyNameBn.Trim());
    }
}
