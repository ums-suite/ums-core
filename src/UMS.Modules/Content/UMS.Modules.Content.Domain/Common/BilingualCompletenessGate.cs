namespace UMS.Modules.Content.Domain.Common;

/// <summary>
/// design-decisions.md "Bilingual-Completeness Gate Enforcement Point": ONE shared domain-layer
/// check function, called independently from every transition path that can move a `Public`-
/// audience <see cref="Notices.Notice"/>/<see cref="Events.Event"/> out of <c>Draft</c> or into
/// <c>Published</c> -
/// <list type="bullet">
/// <item><see cref="Notices.Notice.Schedule"/> (`Draft -&gt; Scheduled`)</item>
/// <item><see cref="Notices.Notice.Publish"/> - called identically by BOTH the manual
/// <c>POST /notices/{id}/publish</c> endpoint AND the scheduled job's own defensive re-check
/// immediately before it commits `Scheduled -&gt; Published` (edge-cases.md "A Notice's translation
/// exists in only one language when publish_at fires") - the job calls the exact same
/// <c>Publish</c> domain method a human's manual-publish call does, so the gate genuinely
/// re-executes at that third moment rather than trusting an earlier check to still hold.</item>
/// <item><see cref="Events.Event.Create"/>/<see cref="Events.Event.UpdateContent"/> - Event has no
/// Draft/Scheduled/Published state machine (§9), so the gate applies directly at
/// create/content-edit time instead.</item>
/// </list>
/// Never a UI-only check (requirement-spec.md §4: "enforced at the domain layer, not just a UI
/// nicety").
/// </summary>
public static class BilingualCompletenessGate
{
    /// <summary>ADR-0011/requirement-spec.md §4.1: the BRD's mandated second language for a bilingual university's public content.</summary>
    public const string RequiredSecondLanguage = "bn";

    /// <summary>
    /// <see langword="true"/> when <paramref name="audience"/> does not include
    /// <see cref="ContentAudience.Public"/> (exempt per requirement-spec.md §2.4: "an internal
    /// Admin-only draft note may remain single-language") or when both the
    /// canonical English fields are non-blank AND a <see cref="RequiredSecondLanguage"/> translation
    /// row is present among <paramref name="translatedLanguageCodes"/>.
    /// </summary>
    public static bool IsSatisfied(ContentAudience audience, string englishTitle, string englishBody, IEnumerable<string> translatedLanguageCodes)
    {
        if (!audience.HasFlag(ContentAudience.Public))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(englishTitle) || string.IsNullOrWhiteSpace(englishBody))
        {
            return false;
        }

        return translatedLanguageCodes.Any(code => string.Equals(code, RequiredSecondLanguage, StringComparison.OrdinalIgnoreCase));
    }
}
