using UMS.Modules.Notifications.Application.Abstractions;
using UMS.Modules.Notifications.Domain.Common;
using UMS.Modules.Notifications.Domain.Templates;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Notifications.Application.Templates;

/// <summary>NTF-7/NTF-8: bilingual, channel-specific, versioned template management - <c>GET /notifications/templates</c>, <c>PUT /notifications/templates/{id}</c>.</summary>
public sealed class TemplateManagementService(ITemplateRepository templates, IUnitOfWork unitOfWork, IClock clock)
{
    public async Task<IReadOnlyList<TemplateDto>> ListAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        var rows = await templates.ListAsync(skip, take, cancellationToken).ConfigureAwait(false);
        return [.. rows.Select(TemplateDto.FromDomain)];
    }

    public async Task<Result<TemplateDto>> GetOrCreateAsync(string eventType, NotificationChannel channel, CancellationToken cancellationToken = default)
    {
        var existing = await templates.GetByEventTypeAndChannelAsync(eventType, channel, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return TemplateDto.FromDomain(existing);
        }

        var createResult = Template.Create(eventType, channel, clock.UtcNow);
        if (createResult.IsFailure)
        {
            return Result.Failure<TemplateDto>(createResult.Error!);
        }

        templates.Add(createResult.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return TemplateDto.FromDomain(createResult.Value);
    }

    /// <summary>Upserts one language's translation on an existing template, identified by id.</summary>
    public async Task<Result<TemplateDto>> UpdateAsync(Guid templateId, UpsertTemplateTranslationCommand command, CancellationToken cancellationToken = default)
    {
        var template = await templates.GetByIdAsync(new TemplateId(templateId), cancellationToken).ConfigureAwait(false);
        if (template is null)
        {
            return Error.NotFound("template.not_found", $"No Template exists with id '{templateId}'.");
        }

        var upsertResult = template.UpsertTranslation(command.LanguageCode, command.Subject, command.Body, command.PushTitle, command.DeepLink, clock.UtcNow);
        if (upsertResult.IsFailure)
        {
            return Result.Failure<TemplateDto>(upsertResult.Error!);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return TemplateDto.FromDomain(template);
    }
}
