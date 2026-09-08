using Microsoft.EntityFrameworkCore;
using UMS.Modules.Alumni.Application.Abstractions;
using UMS.Modules.Alumni.Domain.Chapters;

namespace UMS.Modules.Alumni.Infrastructure.Persistence.Repositories;

internal sealed class AlumniChapterRepository(AlumniDbContext context) : IAlumniChapterRepository
{
    public Task<AlumniChapter?> GetByIdAsync(AlumniChapterId id, CancellationToken cancellationToken = default) =>
        context.Chapters.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AlumniChapter>> ListAsync(int skip, int take, CancellationToken cancellationToken = default) =>
        await context.Chapters.OrderBy(c => c.Name).Skip(skip).Take(take).ToListAsync(cancellationToken).ConfigureAwait(false);

    public void Add(AlumniChapter chapter) => context.Chapters.Add(chapter);
}
