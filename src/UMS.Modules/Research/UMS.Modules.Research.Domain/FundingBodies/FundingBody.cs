using UMS.Modules.Research.Domain.Common;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Research.Domain.FundingBodies;

/// <summary>
/// RES-1: design-decisions.md "FundingBody Modeled as a Reusable Entity, Not a Free-Text Field" -
/// a first-class, Admin/Research-Office-managed reference-data entity, referenced by id from
/// <c>Grant.FundingBodyId</c> rather than re-entered as a free-text string per Grant
/// (ums-conventions.md's anti-primitive-obsession discipline).
/// </summary>
public sealed class FundingBody : AggregateRoot<FundingBodyId>
{
    private FundingBody()
    {
    }

    private FundingBody(FundingBodyId id, string name, string country, FundingBodyType type, string? website, DateTimeOffset now)
    {
        Id = id;
        Name = name;
        Country = country;
        Type = type;
        Website = website;
        CreatedAt = now;
    }

    public string Name { get; private set; } = string.Empty;

    public string Country { get; private set; } = string.Empty;

    public FundingBodyType Type { get; private set; }

    public string? Website { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Result<FundingBody> Create(string name, string country, FundingBodyType type, string? website, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Error.Validation("fundingbody.name_required", "A FundingBody's name is required.");
        }

        if (string.IsNullOrWhiteSpace(country))
        {
            return Error.Validation("fundingbody.country_required", "A FundingBody's country is required.");
        }

        return new FundingBody(FundingBodyId.New(), name.Trim(), country.Trim(), type, string.IsNullOrWhiteSpace(website) ? null : website.Trim(), now);
    }

    public Result Update(string name, string country, FundingBodyType type, string? website)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(Error.Validation("fundingbody.name_required", "A FundingBody's name is required."));
        }

        if (string.IsNullOrWhiteSpace(country))
        {
            return Result.Failure(Error.Validation("fundingbody.country_required", "A FundingBody's country is required."));
        }

        Name = name.Trim();
        Country = country.Trim();
        Type = type;
        Website = string.IsNullOrWhiteSpace(website) ? null : website.Trim();
        return Result.Success();
    }
}
