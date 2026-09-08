using UMS.Modules.Hostel.Application.Abstractions;
using UMS.Modules.Hostel.Domain.ApplicationWindows;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Hostel.Application.ApplicationWindows;

/// <summary>HOS-2: Hostel Officer configuration of the application window and its versioned eligibility rule set (requirement-spec.md §2 step 1, step 3; §9 decision 4).</summary>
public sealed class ApplicationWindowService(IApplicationWindowRepository windows, IUnitOfWork unitOfWork, IClock clock)
{
    public async Task<Result<ApplicationWindowDto>> CreateAsync(CreateApplicationWindowRequest request, CancellationToken cancellationToken = default)
    {
        var created = ApplicationWindow.Create(request.SessionLabel, request.OpensAt, request.ClosesAt, clock.UtcNow);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        windows.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(created.Value);
    }

    public async Task<Result<ApplicationWindowDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var window = await windows.GetByIdAsync(new ApplicationWindowId(id), cancellationToken).ConfigureAwait(false);
        return window is null
            ? Error.NotFound("application_window.not_found", $"No ApplicationWindow exists with id '{id}'.")
            : ToDto(window);
    }

    public async Task<IReadOnlyList<ApplicationWindowDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        (await windows.GetAllAsync(cancellationToken).ConfigureAwait(false)).Select(ToDto).ToList();

    public async Task<Result<ApplicationWindowDto>> ReplaceEligibleProgramsAsync(Guid id, ReplaceEligibleProgramsRequest request, CancellationToken cancellationToken = default)
    {
        var window = await windows.GetByIdAsync(new ApplicationWindowId(id), cancellationToken).ConfigureAwait(false);
        if (window is null)
        {
            return Error.NotFound("application_window.not_found", $"No ApplicationWindow exists with id '{id}'.");
        }

        var replaced = window.ReplaceEligiblePrograms(request.ProgramIds);
        if (replaced.IsFailure)
        {
            return replaced.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(window);
    }

    public async Task<Result<ApplicationWindowDto>> ReplaceEligibleYearsAsync(Guid id, ReplaceEligibleYearsRequest request, CancellationToken cancellationToken = default)
    {
        var window = await windows.GetByIdAsync(new ApplicationWindowId(id), cancellationToken).ConfigureAwait(false);
        if (window is null)
        {
            return Error.NotFound("application_window.not_found", $"No ApplicationWindow exists with id '{id}'.");
        }

        var replaced = window.ReplaceEligibleYears(request.Years);
        if (replaced.IsFailure)
        {
            return replaced.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(window);
    }

    public async Task<Result<ApplicationWindowDto>> ReplaceEligibilityRulesAsync(Guid id, ReplaceEligibilityRulesRequest request, CancellationToken cancellationToken = default)
    {
        var window = await windows.GetByIdAsync(new ApplicationWindowId(id), cancellationToken).ConfigureAwait(false);
        if (window is null)
        {
            return Error.NotFound("application_window.not_found", $"No ApplicationWindow exists with id '{id}'.");
        }

        var rules = new List<EligibilityRuleDefinition>();
        foreach (var ruleRequest in request.Rules)
        {
            if (!Enum.TryParse<HostelEligibilityRuleType>(ruleRequest.RuleType, ignoreCase: true, out var ruleType))
            {
                return Error.Validation("eligibility_rule.invalid_type", $"'{ruleRequest.RuleType}' is not a recognized eligibility rule type.");
            }

            var rule = EligibilityRuleDefinition.Create(ruleType, ruleRequest.Value, ruleRequest.Description);
            if (rule.IsFailure)
            {
                return rule.Error!;
            }

            rules.Add(rule.Value);
        }

        window.ReplaceEligibilityRules(rules);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(window);
    }

    internal static ApplicationWindowDto ToDto(ApplicationWindow window) => new(
        window.Id.Value,
        window.SessionLabel,
        window.OpensAt,
        window.ClosesAt,
        window.EligibleProgramIds.ToList(),
        window.EligibleYears.ToList(),
        window.EligibilityRules.Select(r => new EligibilityRuleDto(r.RuleType.ToString(), r.Value, r.Description)).ToList(),
        window.RulesVersion,
        window.CreatedAt);
}
