using UMS.Modules.Admission.Domain.Tests;

namespace UMS.Modules.Admission.Application.Tests;

public sealed record AdmissionTestDto(Guid Id, Guid CampaignId, string Name, int DurationMinutes, int TotalQuestionCount, int SlotCount);

public sealed record CreateAdmissionTestRequest(Guid CampaignId, string Name, int DurationMinutes);

public sealed record AddQuestionRequest(string Category, QuestionDifficulty Difficulty, string Text, IReadOnlyCollection<string> Options, int? CorrectOptionIndex, bool IsSubjective, decimal MaxScore);

public sealed record SelectionRuleRequest(QuestionDifficulty Difficulty, int Count);

public sealed record AddTestSlotRequest(DateTimeOffset StartAt, DateTimeOffset EndAt, int Capacity);
