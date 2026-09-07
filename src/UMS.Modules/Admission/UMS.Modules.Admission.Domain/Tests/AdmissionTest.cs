using UMS.Modules.Admission.Domain.Common;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Admission.Domain.Tests;

/// <summary>ADM-10: a configured test (question bank, schedule, duration, rules) attached to a Campaign (docs/ddd/ubiquitous-language.md).</summary>
public sealed class AdmissionTest : AggregateRoot<AdmissionTestId>
{
    private readonly List<QuestionBankEntry> _questions = [];
    private readonly List<TestSlot> _slots = [];
    private readonly List<QuestionSelectionRule> _selectionRules = [];

    private AdmissionTest()
    {
    }

    private AdmissionTest(AdmissionTestId id, Guid campaignId, string name, int durationMinutes, DateTimeOffset now)
    {
        Id = id;
        CampaignId = campaignId;
        Name = name;
        DurationMinutes = durationMinutes;
        CreatedAt = now;
    }

    public Guid CampaignId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public int DurationMinutes { get; private set; }

    public IReadOnlyCollection<QuestionBankEntry> Questions => _questions.AsReadOnly();

    public IReadOnlyCollection<TestSlot> Slots => _slots.AsReadOnly();

    public IReadOnlyCollection<QuestionSelectionRule> SelectionRules => _selectionRules.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public int TotalQuestionCount => _selectionRules.Sum(r => r.Count);

    public static Result<AdmissionTest> Create(Guid campaignId, string name, int durationMinutes, DateTimeOffset now)
    {
        if (campaignId == Guid.Empty)
        {
            return Error.Validation("admission_test.campaign_id_required", "An AdmissionTest must reference a campaignId.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Error.Validation("admission_test.name_required", "An AdmissionTest's name is required.");
        }

        if (durationMinutes < 1)
        {
            return Error.Validation("admission_test.duration_invalid", "An AdmissionTest's durationMinutes must be at least 1.");
        }

        return new AdmissionTest(AdmissionTestId.New(), campaignId, name.Trim(), durationMinutes, now);
    }

    public QuestionBankEntry AddQuestion(QuestionBankEntry question)
    {
        ArgumentNullException.ThrowIfNull(question);
        _questions.Add(question);
        return question;
    }

    public Result SetSelectionRule(QuestionDifficulty difficulty, int count)
    {
        var created = QuestionSelectionRule.Create(difficulty, count);
        if (created.IsFailure)
        {
            return created;
        }

        _selectionRules.RemoveAll(r => r.Difficulty == difficulty);
        _selectionRules.Add(created.Value);
        return Result.Success();
    }

    public TestSlot AddTestSlot(DateTimeOffset startAt, DateTimeOffset endAt, int capacity)
    {
        if (endAt <= startAt)
        {
            throw new ArgumentOutOfRangeException(nameof(endAt), "A TestSlot's end must be after its start.");
        }

        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "A TestSlot's capacity must be at least 1.");
        }

        var slot = new TestSlot(TestSlotId.New(), startAt, endAt, capacity);
        _slots.Add(slot);
        return slot;
    }

    /// <summary>
    /// ADM-11/requirement-spec.md §9 decision 4: "question randomization is server-side, seeded,
    /// and the seed is logged (for audit/appeal defensibility)". Deterministic given the same seed -
    /// <see cref="ExamAttempts.ExamAttempt.SeedValue"/> is what a later appeal re-derives this exact
    /// selection from.
    /// </summary>
    public Result<IReadOnlyList<QuestionId>> SelectQuestions(long seed)
    {
        if (_selectionRules.Count == 0)
        {
            return Error.Conflict("admission_test.no_selection_rules", $"AdmissionTest '{Id}' has no QuestionSelectionRules configured.");
        }

        var random = new Random((int)(seed % int.MaxValue));
        var selected = new List<QuestionId>();

        foreach (var rule in _selectionRules)
        {
            var pool = _questions.Where(q => q.Difficulty == rule.Difficulty).ToList();
            if (pool.Count < rule.Count)
            {
                return Error.Conflict("admission_test.insufficient_questions", $"AdmissionTest '{Id}' has only {pool.Count} '{rule.Difficulty}' question(s) but its selection rule requires {rule.Count}.");
            }

            selected.AddRange(pool.OrderBy(_ => random.Next()).Take(rule.Count).Select(q => q.Id));
        }

        return selected;
    }
}
