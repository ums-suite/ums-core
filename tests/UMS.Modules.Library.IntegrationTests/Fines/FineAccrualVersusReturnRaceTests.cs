using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Library.Application.Fines;
using UMS.Modules.Library.Application.Loans;
using UMS.Modules.Library.Domain.Common;
using UMS.Modules.Library.Infrastructure.Persistence;
using UMS.Modules.Library.IntegrationTests.Infrastructure;

namespace UMS.Modules.Library.IntegrationTests.Fines;

/// <summary>
/// LIB-11: edge-cases.md "Fine-accrual job racing a book return" - the accrual job row-locks the Loan
/// and re-checks <c>Status == Active</c> post-lock, so a return committing between the job's
/// unlocked candidate read and its lock acquisition is caught, not silently double-charged
/// (design-decisions.md "Fine-Accrual Job Idempotency").
/// </summary>
[Collection(LibraryApiTestCollectionDefinition.Name)]
public sealed class FineAccrualVersusReturnRaceTests(LibraryServiceFixture fixture)
{
    [Fact]
    public async Task Concurrent_accrual_sweep_and_return_never_double_charge_and_never_accrue_after_return()
    {
        var (_, bookCopyId) = await LibraryTestData.CreateBookWithSingleCopyAsync(fixture.Services);
        var studentId = Guid.NewGuid();
        LibraryTestData.RegisterActiveStudent(fixture, studentId, Guid.NewGuid());

        Guid loanId;
        using (var setupScope = fixture.Services.CreateScope())
        {
            var loanService = setupScope.ServiceProvider.GetRequiredService<LoanService>();
            var issued = await loanService.IssueAsync(bookCopyId, studentId, BorrowerType.Student, issuedByUserId: Guid.NewGuid(), correlationId: Guid.NewGuid().ToString());
            Assert.True(issued.IsSuccess);
            loanId = issued.Value.Id;

            // Test-setup-only backdating (never how the mechanism itself is tested) - forces this
            // Loan into the overdue candidate set immediately, rather than waiting real days.
            var dbContext = setupScope.ServiceProvider.GetRequiredService<LibraryDbContext>();
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"UPDATE library.loans SET due_date = {DateTimeOffset.UtcNow.AddDays(-1)} WHERE id = {loanId}");
        }

        async Task RunAccrualSweepAsync()
        {
            using var scope = fixture.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<FineAccrualService>();
            await service.AccrueAsync(batchSize: 10);
        }

        async Task ReturnAsync()
        {
            using var scope = fixture.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<LoanService>();
            var result = await service.ReturnAsync(loanId, actorUserId: Guid.NewGuid(), correlationId: Guid.NewGuid().ToString());
            Assert.True(result.IsSuccess);
        }

        await Task.WhenAll(RunAccrualSweepAsync(), ReturnAsync());

        using var verifyScope = fixture.Services.CreateScope();
        var verifyLoanService = verifyScope.ServiceProvider.GetRequiredService<LoanService>();
        var loanAfter = (await verifyLoanService.GetByIdAsync(loanId)).Value;
        Assert.Equal("Returned", loanAfter.Status);

        var fineService = verifyScope.ServiceProvider.GetRequiredService<FineService>();
        var finesForBorrower = await fineService.GetByBorrowerAsync(studentId);

        // Either the sweep won the race (one Fine, one accrual increment) or the return won (zero
        // Fines) - never more than one Fine, and never a Fine whose amount implies a double-charge.
        Assert.True(finesForBorrower.Count <= 1);
        if (finesForBorrower.Count == 1)
        {
            Assert.Equal(10m, finesForBorrower[0].Amount);
        }

        // A second sweep pass (simulating the next scheduled run) must never accrue against an
        // already-Returned Loan.
        using var secondSweepScope = fixture.Services.CreateScope();
        var secondSweep = secondSweepScope.ServiceProvider.GetRequiredService<FineAccrualService>();
        await secondSweep.AccrueAsync(batchSize: 10);

        var finesAfterSecondSweep = await fineService.GetByBorrowerAsync(studentId);
        Assert.Equal(finesForBorrower.Count, finesAfterSecondSweep.Count);
    }
}
