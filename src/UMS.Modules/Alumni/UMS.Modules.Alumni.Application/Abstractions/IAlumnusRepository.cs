using UMS.Modules.Alumni.Domain.Alumni;

namespace UMS.Modules.Alumni.Application.Abstractions;

public interface IAlumnusRepository
{
    public Task<Alumnus?> GetByIdAsync(AlumnusId id, CancellationToken cancellationToken = default);

    public Task<Alumnus?> GetByStudentIdRefAsync(Guid studentIdRef, CancellationToken cancellationToken = default);

    /// <param name="includePrivate">Admin/support bypass (requirement-spec.md §2.2/§7, <c>alumni.directory.read.private</c>) - when <see langword="false"/>, only <see cref="ProfileVisibility.Public"/> records match regardless of the other filters.</param>
    /// <param name="alwaysIncludeAlumnusId">The caller's own <see cref="AlumnusId"/>, if any - a caller's own record always matches even while Private (requirement-spec.md §2.2: "except ... the alumnus's own record").</param>
    public Task<IReadOnlyList<Alumnus>> SearchDirectoryAsync(AlumniDirectoryFilter filter, bool includePrivate, AlumnusId? alwaysIncludeAlumnusId, int skip, int take, CancellationToken cancellationToken = default);

    public void Add(Alumnus alumnus);
}

/// <summary>ALM-3: requirement-spec.md §2.2 directory filters - every filter is optional/additive.</summary>
public sealed record AlumniDirectoryFilter(int? GraduationYear, Guid? ProgramId, Guid? DepartmentId, Guid? ChapterId, string? Employer, string? Location);
