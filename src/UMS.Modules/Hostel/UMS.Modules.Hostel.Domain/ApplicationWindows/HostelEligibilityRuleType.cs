namespace UMS.Modules.Hostel.Domain.ApplicationWindows;

/// <summary>requirement-spec.md §2 step 3: "e.g. minimum year of study, home-district distance, financial-need flag". A first-pass, configurable rule catalog (§9 decision 4) - not a fixed formula.</summary>
public enum HostelEligibilityRuleType
{
    /// <summary>Passes when the Student's self-declared year of study is greater than or equal to <see cref="EligibilityRuleDefinition.Value"/>.</summary>
    MinimumYearOfStudy,

    /// <summary>Passes only when the Student's self-declared financial-need flag is set; <see cref="EligibilityRuleDefinition.Value"/> is unused (any non-null value enables the rule).</summary>
    FinancialNeedRequired,

    /// <summary>Passes when the Student's self-declared home-district distance (km) is greater than or equal to <see cref="EligibilityRuleDefinition.Value"/> - i.e. a minimum-distance-from-campus test.</summary>
    MinimumHomeDistrictDistanceKm,
}
