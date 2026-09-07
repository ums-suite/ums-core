using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Admission.Domain.Tests;

/// <summary>How many questions of a given difficulty an ExamAttempt draws from the bank (requirement-spec.md §2: "random selection per difficulty").</summary>
public sealed record QuestionSelectionRule
{
    private QuestionSelectionRule(QuestionDifficulty difficulty, int count)
    {
        Difficulty = difficulty;
        Count = count;
    }

    // EF Core materialization only (PropertyAccessMode.Field) - never called from application code.
    private QuestionSelectionRule()
    {
    }

    public QuestionDifficulty Difficulty { get; }

    public int Count { get; }

    public static Result<QuestionSelectionRule> Create(QuestionDifficulty difficulty, int count) =>
        count < 1
            ? Error.Validation("question_selection_rule.count_invalid", "A QuestionSelectionRule's count must be at least 1.")
            : new QuestionSelectionRule(difficulty, count);
}
