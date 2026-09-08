using UMS.Modules.Alumni.Application.Abstractions;
using UMS.Modules.Alumni.Domain.Alumni;
using UMS.Modules.Alumni.Domain.Chapters;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Alumni.Application.Chapters;

/// <summary>ALM-4: chapter create/list/join/leave (requirement-spec.md §2.6, §6).</summary>
public sealed class ChapterService(IAlumniChapterRepository chapters, IUnitOfWork unitOfWork, IClock clock)
{
    public static ChapterDto ToDto(AlumniChapter chapter) => new(chapter.Id.Value, chapter.Name, chapter.Description, chapter.Region, chapter.Memberships.Count, chapter.CreatedAt);

    public async Task<Result<ChapterDto>> CreateAsync(CreateChapterRequest request, CancellationToken cancellationToken = default)
    {
        AlumniChapter chapter;
        try
        {
            chapter = AlumniChapter.Create(request.Name, request.Description, request.Region, clock.UtcNow);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("chapter.invalid", ex.Message);
        }

        chapters.Add(chapter);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(chapter);
    }

    public async Task<IReadOnlyList<ChapterDto>> ListAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take <= 0 ? 50 : take, 1, 200);
        var items = await chapters.ListAsync(skip, take, cancellationToken).ConfigureAwait(false);
        return items.Select(ToDto).ToList();
    }

    public async Task<Result<ChapterDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var chapter = await chapters.GetByIdAsync(new AlumniChapterId(id), cancellationToken).ConfigureAwait(false);
        return chapter is null ? Error.NotFound("chapter.not_found", $"No AlumniChapter exists with id '{id}'.") : ToDto(chapter);
    }

    /// <summary>requirement-spec.md §2.6: an Alumnus joins/leaves voluntarily.</summary>
    public async Task<Result<ChapterDto>> JoinAsync(Guid chapterId, Guid alumnusId, CancellationToken cancellationToken = default)
    {
        var chapter = await chapters.GetByIdAsync(new AlumniChapterId(chapterId), cancellationToken).ConfigureAwait(false);
        if (chapter is null)
        {
            return Error.NotFound("chapter.not_found", $"No AlumniChapter exists with id '{chapterId}'.");
        }

        chapter.Join(new AlumnusId(alumnusId), clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(chapter);
    }

    public async Task<Result<ChapterDto>> LeaveAsync(Guid chapterId, Guid alumnusId, CancellationToken cancellationToken = default)
    {
        var chapter = await chapters.GetByIdAsync(new AlumniChapterId(chapterId), cancellationToken).ConfigureAwait(false);
        if (chapter is null)
        {
            return Error.NotFound("chapter.not_found", $"No AlumniChapter exists with id '{chapterId}'.");
        }

        chapter.Leave(new AlumnusId(alumnusId));
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(chapter);
    }
}
