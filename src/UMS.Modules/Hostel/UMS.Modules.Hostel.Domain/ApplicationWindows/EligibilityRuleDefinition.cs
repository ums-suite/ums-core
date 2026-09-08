using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Hostel.Domain.ApplicationWindows;

/// <summary>
/// One rule in an <see cref="ApplicationWindow"/>'s versioned, configurable eligibility rule set
/// (requirement-spec.md §9 decision 4, mirroring Admission's own <c>EligibilityRule</c> shape).
/// Stored as a JSONB column on <see cref="ApplicationWindow"/> (not a relational owned collection) -
/// this is configuration data read as a whole set, never individually queried/locked, so JSONB
/// storage avoids the EF Core 10 owned-collection shadow-key ceremony entirely for something that
/// doesn't need it.
/// </summary>
public sealed record EligibilityRuleDefinition(HostelEligibilityRuleType RuleType, decimal Value, string Description)
{
    public static Result<EligibilityRuleDefinition> Create(HostelEligibilityRuleType ruleType, decimal value, string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return Error.Validation("eligibility_rule.description_required", "An EligibilityRule's description is required.");
        }

        return new EligibilityRuleDefinition(ruleType, value, description.Trim());
    }
}
