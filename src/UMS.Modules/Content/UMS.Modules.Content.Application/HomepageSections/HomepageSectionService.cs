using UMS.Modules.Content.Application.Abstractions;
using UMS.Modules.Content.Domain.Common;
using UMS.Modules.Content.Domain.HomepageSections;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Organization;

namespace UMS.Modules.Content.Application.HomepageSections;

/// <summary>
/// CNT-10: ordered/toggleable homepage layout, resolving a cross-module Organization reference
/// (e.g. "featured Program") at read time - requirement-spec.md §2.5: "never a denormalized copy
/// that can drift" (ADR-0002). edge-cases.md "Organization reference later deleted/deactivated":
/// <see cref="IOrganizationNodeExistenceChecker.ExistsAsync"/> failing to resolve degrades to
/// <see cref="HomepageSectionDto.ReferenceAvailable"/> = <see langword="false"/>, never a 500.
/// </summary>
public sealed class HomepageSectionService(IHomepageSectionRepository sections, IUnitOfWork unitOfWork, IOrganizationNodeExistenceChecker organizationNodes, IClock clock)
{
    public async Task<HomepageSectionDto> ToDtoAsync(HomepageSection section, CancellationToken cancellationToken)
    {
        bool? referenceAvailable = null;
        if (section.ReferenceOrganizationNodeId is { } nodeId)
        {
            try
            {
                referenceAvailable = await organizationNodes.ExistsAsync(nodeId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // edge-cases.md: never let a downstream Organization lookup failure surface as a
                // 500 on an otherwise-unrelated homepage read - degrade to "unavailable" instead.
                referenceAvailable = false;
            }
        }

        return new HomepageSectionDto(
            section.Id.Value,
            section.SectionKey,
            section.Title,
            section.SortOrder,
            section.IsEnabled,
            section.ReferenceOrganizationNodeId,
            referenceAvailable,
            section.CreatedAt,
            section.UpdatedAt,
            section.Version);
    }

    public async Task<Result<HomepageSectionDto>> CreateAsync(string sectionKey, string title, int sortOrder, bool isEnabled, Guid? referenceOrganizationNodeId, CancellationToken cancellationToken = default)
    {
        var created = HomepageSection.Create(sectionKey, title, sortOrder, isEnabled, referenceOrganizationNodeId, clock.UtcNow);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        sections.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await ToDtoAsync(created.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>CNT-10: `PUT /content/homepage-sections` - Admin reorder/toggle, one section at a time (each carries its own optimistic-concurrency version).</summary>
    public async Task<Result<HomepageSectionDto>> UpdateAsync(Guid id, string title, int sortOrder, bool isEnabled, Guid? referenceOrganizationNodeId, uint expectedVersion, CancellationToken cancellationToken = default)
    {
        var section = await sections.GetByIdAsync(new HomepageSectionId(id), cancellationToken).ConfigureAwait(false);
        if (section is null)
        {
            return Error.NotFound("homepage_section.not_found", $"No HomepageSection exists with id '{id}'.");
        }

        var updated = section.UpdateDetails(title, sortOrder, isEnabled, referenceOrganizationNodeId, clock.UtcNow);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        unitOfWork.SetExpectedVersion(section, expectedVersion);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Error.Conflict("homepage_section.concurrency_conflict", ex.Message);
        }

        return await ToDtoAsync(section, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Public read: enabled sections only, in <see cref="ContentOrdering.ByDisplayOrder{T}(IEnumerable{T})"/> order (requirement-spec.md §4, shared with Banner).</summary>
    public async Task<IReadOnlyList<HomepageSectionDto>> ListEnabledAsync(CancellationToken cancellationToken = default)
    {
        var ordered = (await sections.ListEnabledOrderedAsync(cancellationToken).ConfigureAwait(false)).ByDisplayOrder();
        var dtos = new List<HomepageSectionDto>();
        foreach (var section in ordered)
        {
            dtos.Add(await ToDtoAsync(section, cancellationToken).ConfigureAwait(false));
        }

        return dtos;
    }

    public async Task<IReadOnlyList<HomepageSectionDto>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var ordered = (await sections.ListAllOrderedAsync(cancellationToken).ConfigureAwait(false)).ByDisplayOrder();
        var dtos = new List<HomepageSectionDto>();
        foreach (var section in ordered)
        {
            dtos.Add(await ToDtoAsync(section, cancellationToken).ConfigureAwait(false));
        }

        return dtos;
    }
}
