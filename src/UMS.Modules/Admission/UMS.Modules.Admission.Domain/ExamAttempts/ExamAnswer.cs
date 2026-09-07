using UMS.Modules.Admission.Domain.Tests;

namespace UMS.Modules.Admission.Domain.ExamAttempts;

/// <summary>ADM-12: one auto-saved response - batched/debounced at the caller (requirement-spec.md §5), never one DB round-trip per keystroke; this entity itself is agnostic to that batching.</summary>
public sealed class ExamAnswer
{
    internal ExamAnswer(QuestionId questionId, int? selectedOptionIndex, string? subjectiveText, DateTimeOffset answeredAt)
    {
        QuestionId = questionId;
        SelectedOptionIndex = selectedOptionIndex;
        SubjectiveText = subjectiveText;
        AnsweredAt = answeredAt;
    }

    private ExamAnswer()
    {
    }

    public QuestionId QuestionId { get; private set; }

    public int? SelectedOptionIndex { get; private set; }

    public string? SubjectiveText { get; private set; }

    public DateTimeOffset AnsweredAt { get; private set; }

    internal void Update(int? selectedOptionIndex, string? subjectiveText, DateTimeOffset answeredAt)
    {
        SelectedOptionIndex = selectedOptionIndex;
        SubjectiveText = subjectiveText;
        AnsweredAt = answeredAt;
    }
}
