using UMS.Modules.Alumni.Domain.Chapters;

namespace UMS.Modules.Alumni.Application.Abstractions;

public interface IAlumniChapterRepository
{
    public Task<AlumniChapter?> GetByIdAsync(AlumniChapterId id, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<AlumniChapter>> ListAsync(int skip, int take, CancellationToken cancellationToken = default);

    public void Add(AlumniChapter chapter);
}
