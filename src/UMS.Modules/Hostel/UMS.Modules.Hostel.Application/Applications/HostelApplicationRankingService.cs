using UMS.Modules.Hostel.Application.Abstractions;
using UMS.Modules.Hostel.Application.Common;
using UMS.Modules.Hostel.Domain.Applications;
using UMS.Modules.Hostel.Domain.ApplicationWindows;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Hostel.Application.Applications;

/// <summary>
/// HOS-4: eligibility computation and ranking engine (requirement-spec.md §2 steps 3-4; §3
/// <c>HostelApplicationRanked</c>, internal, drives the Officer review queue; §4 invariant "cannot
/// reach Approved without a computed, passing eligibility result").
///
/// <para>
/// <b>Scoring formula - a documented first-pass default, not a fixed platform rule</b>
/// (requirement-spec.md §9 decision 4: "a configurable, versioned rule set... since the BRD only
/// names 'eligibility calculated' and 'ranked' without a fixed formula"). Score =
/// <c>(YearOfStudy * 10) + (HasFinancialNeed ? 50 : 0) + HomeDistrictDistanceKm</c> - higher is
/// higher priority. The Hostel Office may retune this in a future revision; nothing else in the
/// domain model depends on the concrete weights.
/// </para>
/// </summary>
public sealed class HostelApplicationRankingService(
    IHostelApplicationRepository applications,
    IApplicationWindowRepository windows,
    StudentContextService studentContext,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<int>> RankWindowAsync(Guid applicationWindowId, CancellationToken cancellationToken = default)
    {
        var window = await windows.GetByIdAsync(new ApplicationWindowId(applicationWindowId), cancellationToken).ConfigureAwait(false);
        if (window is null)
        {
            return Error.NotFound("application_window.not_found", $"No ApplicationWindow exists with id '{applicationWindowId}'.");
        }

        var submitted = await applications.GetByWindowAndStatusAsync(applicationWindowId, HostelApplicationStatus.Submitted, cancellationToken).ConfigureAwait(false);
        if (submitted.Count == 0)
        {
            return 0;
        }

        var scored = new List<(HostelApplication Application, decimal Score, bool IsEligible)>();
        foreach (var application in submitted)
        {
            var standing = await studentContext.GetStandingAsync(application.StudentId, cancellationToken).ConfigureAwait(false);
            var isEligible = IsEligible(window, application, standing);
            var score = ComputeScore(application);
            scored.Add((application, score, isEligible));
        }

        var now = clock.UtcNow;
        var rankPosition = 1;
        foreach (var (application, score, isEligible) in scored.OrderByDescending(s => s.IsEligible).ThenByDescending(s => s.Score).ThenBy(s => s.Application.SubmittedAt))
        {
            var position = isEligible ? rankPosition++ : (int?)null;
            application.MarkRanked(score, isEligible, position, now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return scored.Count;
    }

    private static bool IsEligible(ApplicationWindow window, HostelApplication application, UMS.Shared.Student.StudentAcademicStanding? standing)
    {
        if (standing is null || !string.Equals(standing.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!window.AllowsProgram(standing.ProgramId) || !window.AllowsYearOfStudy(application.YearOfStudy))
        {
            return false;
        }

        foreach (var rule in window.EligibilityRules)
        {
            var passes = rule.RuleType switch
            {
                HostelEligibilityRuleType.MinimumYearOfStudy => application.YearOfStudy >= rule.Value,
                HostelEligibilityRuleType.FinancialNeedRequired => application.HasFinancialNeed,
                HostelEligibilityRuleType.MinimumHomeDistrictDistanceKm => (application.HomeDistrictDistanceKm ?? 0) >= rule.Value,
                _ => true,
            };

            if (!passes)
            {
                return false;
            }
        }

        return true;
    }

    private static decimal ComputeScore(HostelApplication application) =>
        (application.YearOfStudy * 10m) + (application.HasFinancialNeed ? 50m : 0m) + (application.HomeDistrictDistanceKm ?? 0m);
}
