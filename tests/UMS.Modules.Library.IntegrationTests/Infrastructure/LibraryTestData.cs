using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Library.Application.Catalog;
using UMS.Modules.Library.Domain.Common;

namespace UMS.Modules.Library.IntegrationTests.Infrastructure;

/// <summary>Seeds test fixtures through the real Application services (never raw SQL), mirroring Hostel's/Finance's own <c>*TestData</c> helper pattern.</summary>
internal static class LibraryTestData
{
    public static async Task<Guid> CreateBookAsync(IServiceProvider services, string title = "Test Book", bool isOpenAccessDigital = false)
    {
        using var scope = services.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogService>();

        var book = (await catalog.CreateBookAsync(new CreateBookRequest($"{title}-{Guid.NewGuid():N}", null, null, null, null, isOpenAccessDigital))).Value;
        return book.Id;
    }

    public static async Task<Guid> CreateBookCopyAsync(IServiceProvider services, Guid bookId, string copyType = "Physical")
    {
        using var scope = services.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogService>();

        var copy = (await catalog.CreateBookCopyAsync(new CreateBookCopyRequest(bookId, $"ACC-{Guid.NewGuid():N}"[..12], "Good", copyType))).Value;
        return copy.Id;
    }

    /// <summary>A Book with exactly one Physical BookCopy - the common "single scarce copy" fixture shape the concurrency tests need.</summary>
    public static async Task<(Guid BookId, Guid BookCopyId)> CreateBookWithSingleCopyAsync(IServiceProvider services)
    {
        var bookId = await CreateBookAsync(services).ConfigureAwait(false);
        var copyId = await CreateBookCopyAsync(services, bookId).ConfigureAwait(false);
        return (bookId, copyId);
    }

    public static void RegisterActiveStudent(LibraryServiceFixture fixture, Guid studentId, Guid identityUserId) =>
        fixture.StudentStatusChecker.Register(new UMS.Shared.Student.StudentAcademicStanding(studentId, Guid.NewGuid(), Guid.NewGuid(), "Active", identityUserId));

    public static void RegisterFacultyStatus(LibraryServiceFixture fixture, Guid facultyMemberId, Guid identityUserId, string status = "Active") =>
        fixture.FacultyMemberLookup.Register(new UMS.Shared.Faculty.FacultyMemberSummary(facultyMemberId, Guid.NewGuid(), status), identityUserId);
}
