using UMS.Modules.Admission.Application.Abstractions;
using UMS.Modules.Admission.Domain.Tests;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Admission.Application.Tests;

/// <summary>ADM-10: Admission Test Lifecycle configuration (requirement-spec.md §2, §3).</summary>
public sealed class AdmissionTestService(IAdmissionTestRepository tests, IUnitOfWork unitOfWork, IClock clock)
{
    public async Task<Result<AdmissionTestDto>> CreateAsync(CreateAdmissionTestRequest request, CancellationToken cancellationToken = default)
    {
        var created = AdmissionTest.Create(request.CampaignId, request.Name, request.DurationMinutes, clock.UtcNow);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        tests.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(created.Value);
    }

    public async Task<Result<AdmissionTestDto>> AddQuestionAsync(Guid testId, AddQuestionRequest request, CancellationToken cancellationToken = default)
    {
        var test = await tests.GetByIdAsync(new AdmissionTestId(testId), cancellationToken).ConfigureAwait(false);
        if (test is null)
        {
            return Error.NotFound("admission_test.not_found", $"No AdmissionTest exists with id '{testId}'.");
        }

        var question = QuestionBankEntry.Create(request.Category, request.Difficulty, request.Text, request.Options, request.CorrectOptionIndex, request.IsSubjective, request.MaxScore);
        if (question.IsFailure)
        {
            return question.Error!;
        }

        test.AddQuestion(question.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(test);
    }

    public async Task<Result<AdmissionTestDto>> SetSelectionRuleAsync(Guid testId, SelectionRuleRequest request, CancellationToken cancellationToken = default)
    {
        var test = await tests.GetByIdAsync(new AdmissionTestId(testId), cancellationToken).ConfigureAwait(false);
        if (test is null)
        {
            return Error.NotFound("admission_test.not_found", $"No AdmissionTest exists with id '{testId}'.");
        }

        var set = test.SetSelectionRule(request.Difficulty, request.Count);
        if (set.IsFailure)
        {
            return set.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(test);
    }

    public async Task<Result<AdmissionTestDto>> AddTestSlotAsync(Guid testId, AddTestSlotRequest request, CancellationToken cancellationToken = default)
    {
        var test = await tests.GetByIdAsync(new AdmissionTestId(testId), cancellationToken).ConfigureAwait(false);
        if (test is null)
        {
            return Error.NotFound("admission_test.not_found", $"No AdmissionTest exists with id '{testId}'.");
        }

        test.AddTestSlot(request.StartAt, request.EndAt, request.Capacity);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(test);
    }

    public async Task<Result<AdmissionTestDto>> GetByIdAsync(Guid testId, CancellationToken cancellationToken = default)
    {
        var test = await tests.GetByIdAsync(new AdmissionTestId(testId), cancellationToken).ConfigureAwait(false);
        return test is null
            ? Error.NotFound("admission_test.not_found", $"No AdmissionTest exists with id '{testId}'.")
            : ToDto(test);
    }

    public async Task<Result<AdmissionTestDto>> GetByCampaignIdAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        var test = await tests.GetByCampaignIdAsync(campaignId, cancellationToken).ConfigureAwait(false);
        return test is null
            ? Error.NotFound("admission_test.not_found", $"No AdmissionTest exists for Campaign '{campaignId}'.")
            : ToDto(test);
    }

    internal static AdmissionTestDto ToDto(AdmissionTest test) => new(test.Id.Value, test.CampaignId, test.Name, test.DurationMinutes, test.TotalQuestionCount, test.Slots.Count);
}
