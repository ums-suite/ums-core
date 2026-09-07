using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Admission.Domain.Tests;

/// <summary>ADM-10: one entry in an AdmissionTest's own question bank, categorized by subject and difficulty (requirement-spec.md §2 Admission Test Lifecycle).</summary>
public sealed class QuestionBankEntry
{
    private readonly List<string> _options = [];

    internal QuestionBankEntry(QuestionId id, string category, QuestionDifficulty difficulty, string text, IReadOnlyCollection<string> options, int? correctOptionIndex, bool isSubjective, decimal maxScore)
    {
        Id = id;
        Category = category;
        Difficulty = difficulty;
        Text = text;
        _options.AddRange(options);
        CorrectOptionIndex = correctOptionIndex;
        IsSubjective = isSubjective;
        MaxScore = maxScore;
    }

    public QuestionId Id { get; private set; }

    public string Category { get; private set; } = string.Empty;

    public QuestionDifficulty Difficulty { get; private set; }

    public string Text { get; private set; } = string.Empty;

    public IReadOnlyCollection<string> Options => _options.AsReadOnly();

    /// <summary>Objective questions only - <c>null</c> when <see cref="IsSubjective"/> (evaluated manually, requirement-spec.md §2: "auto-scored for objective questions; manual for any subjective component").</summary>
    public int? CorrectOptionIndex { get; private set; }

    public bool IsSubjective { get; private set; }

    public decimal MaxScore { get; private set; }

    public static Result<QuestionBankEntry> Create(string category, QuestionDifficulty difficulty, string text, IReadOnlyCollection<string> options, int? correctOptionIndex, bool isSubjective, decimal maxScore)
    {
        if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(text))
        {
            return Error.Validation("question.fields_required", "A question's category and text are both required.");
        }

        if (maxScore <= 0)
        {
            return Error.Validation("question.max_score_invalid", "A question's maxScore must be positive.");
        }

        if (!isSubjective)
        {
            if (options is not { Count: >= 2 })
            {
                return Error.Validation("question.options_required", "An objective question requires at least two options.");
            }

            if (correctOptionIndex is null || correctOptionIndex < 0 || correctOptionIndex >= options.Count)
            {
                return Error.Validation("question.correct_option_invalid", "An objective question's correctOptionIndex must reference one of its own options.");
            }
        }

        return new QuestionBankEntry(QuestionId.New(), category.Trim(), difficulty, text.Trim(), options, isSubjective ? null : correctOptionIndex, isSubjective, maxScore);
    }
}
