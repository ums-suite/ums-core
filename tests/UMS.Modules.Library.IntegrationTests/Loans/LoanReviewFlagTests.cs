using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Library.Application.Loans;
using UMS.Modules.Library.Domain.Common;
using UMS.Modules.Library.IntegrationTests.Infrastructure;

namespace UMS.Modules.Library.IntegrationTests.Loans;

/// <summary>LIB-16: requirement-spec.md §8 "A Faculty member's employment status changes to inactive while holding a Loan → flagged for a recall notice, not auto-returned" - and the identical treatment for a Student.</summary>
[Collection(LibraryApiTestCollectionDefinition.Name)]
public sealed class LoanReviewFlagTests(LibraryServiceFixture fixture)
{
    [Fact]
    public async Task A_Student_going_inactive_flags_their_open_Loan_but_never_auto_returns_it()
    {
        var (_, bookCopyId) = await LibraryTestData.CreateBookWithSingleCopyAsync(fixture.Services);
        var studentId = Guid.NewGuid();
        LibraryTestData.RegisterActiveStudent(fixture, studentId, Guid.NewGuid());

        using var scope = fixture.Services.CreateScope();
        var loanService = scope.ServiceProvider.GetRequiredService<LoanService>();
        var issued = await loanService.IssueAsync(bookCopyId, studentId, BorrowerType.Student, issuedByUserId: Guid.NewGuid(), correlationId: Guid.NewGuid().ToString());
        Assert.True(issued.IsSuccess);

        // Re-register the same Student with a non-Active status - simulates a StudentStatusChanged
        // event's current-status re-resolution (the envelope itself only ever carries the id).
        fixture.StudentStatusChecker.Register(new UMS.Shared.Student.StudentAcademicStanding(studentId, Guid.NewGuid(), Guid.NewGuid(), "Suspended", Guid.NewGuid()));

        var reviewFlagService = scope.ServiceProvider.GetRequiredService<LoanReviewFlagService>();
        await reviewFlagService.ApplyStudentStatusChangeAsync(studentId, $"StudentStatusChanged:{Guid.NewGuid()}");

        var flags = await reviewFlagService.GetByLoanAsync(issued.Value.Id);
        Assert.Single(flags);
        Assert.Contains("Suspended", flags[0].Reason);

        // Advisory only - the Loan itself is untouched.
        var loanAfter = (await loanService.GetByIdAsync(issued.Value.Id)).Value;
        Assert.Equal("Active", loanAfter.Status);
    }

    [Fact]
    public async Task A_FacultyMember_going_inactive_flags_their_open_Loan()
    {
        var (_, bookCopyId) = await LibraryTestData.CreateBookWithSingleCopyAsync(fixture.Services);
        var facultyMemberId = Guid.NewGuid();
        var identityUserId = Guid.NewGuid();
        LibraryTestData.RegisterFacultyStatus(fixture, facultyMemberId, identityUserId, "Active");

        using var scope = fixture.Services.CreateScope();
        var loanService = scope.ServiceProvider.GetRequiredService<LoanService>();
        var issued = await loanService.IssueAsync(bookCopyId, facultyMemberId, BorrowerType.Faculty, issuedByUserId: Guid.NewGuid(), correlationId: Guid.NewGuid().ToString());
        Assert.True(issued.IsSuccess);

        LibraryTestData.RegisterFacultyStatus(fixture, facultyMemberId, identityUserId, "Separated");

        var reviewFlagService = scope.ServiceProvider.GetRequiredService<LoanReviewFlagService>();
        await reviewFlagService.ApplyFacultyStatusChangeAsync(facultyMemberId, $"FacultyMemberStatusChanged:{Guid.NewGuid()}");

        var flags = await reviewFlagService.GetByLoanAsync(issued.Value.Id);
        Assert.Single(flags);
        Assert.Contains("Separated", flags[0].Reason);
    }
}
