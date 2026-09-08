using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Hostel.Application.Allocations;
using UMS.Modules.Hostel.Application.Applications;
using UMS.Modules.Hostel.IntegrationTests.Infrastructure;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Hostel.IntegrationTests.Applications;

/// <summary>
/// edge-cases.md "HostelApplication withdrawn while its allocation is mid-approval" -
/// design-decisions.md "HostelApplication Withdrawal as a First-Class State Transition": both the
/// withdrawal command and the review-approval command take <c>SELECT ... FOR UPDATE</c> on the same
/// <c>HostelApplication</c> row, so they serialize rather than race. A real concurrent
/// <see cref="Task.WhenAll"/> against Postgres.
/// </summary>
[Collection(HostelApiTestCollectionDefinition.Name)]
public sealed class WithdrawalVersusApprovalRaceTests(HostelServiceFixture fixture)
{
    [Fact]
    public async Task Concurrent_withdraw_and_approve_against_the_same_application_resolve_to_exactly_one_winner()
    {
        var (hostelId, _, _) = await HostelTestData.CreateHostelWithSingleBedRoomAsync(fixture.Services);
        var windowId = await HostelTestData.CreateOpenApplicationWindowAsync(fixture.Services);
        var studentId = Guid.NewGuid();
        var applicationId = await HostelTestData.SubmitAndRankApplicationAsync(fixture.Services, studentId, hostelId, windowId);

        async Task<Result> WithdrawAsync()
        {
            using var scope = fixture.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<HostelApplicationService>();
            return await service.WithdrawAsync(applicationId, studentId, Guid.NewGuid().ToString());
        }

        async Task<Result<AllocationDto>> ApproveAsync()
        {
            using var scope = fixture.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<AllocationService>();
            return await service.ApproveAndAllocateAsync(applicationId, Guid.NewGuid(), Guid.NewGuid().ToString());
        }

        var withdrawTask = WithdrawAsync();
        var approveTask = ApproveAsync();
        await Task.WhenAll(withdrawTask, approveTask);
        var withdrawResult = await withdrawTask;
        var approveResult = await approveTask;

        using var verifyScope = fixture.Services.CreateScope();
        var applications = verifyScope.ServiceProvider.GetRequiredService<HostelApplicationService>();
        var finalState = (await applications.GetByIdAsync(applicationId)).Value;

        if (withdrawResult.IsSuccess)
        {
            // Withdrawal won the lock race first - the approval must have lost against the
            // now-Withdrawn row (never silently succeeded, never left the Application ambiguous).
            Assert.Equal("Withdrawn", finalState.Status);
            Assert.True(approveResult.IsFailure);
            Assert.Equal("hostel_application.not_reviewable", approveResult.Error!.Code);
        }
        else
        {
            // Approval won the lock race first - requirement-spec.md §4: "Approved" is now terminal
            // for this path; the trade-off design-decisions.md accepts is exactly this rejection.
            Assert.Equal("Approved", finalState.Status);
            Assert.True(approveResult.IsSuccess);
            Assert.Equal("hostel_application.not_withdrawable", withdrawResult.Error!.Code);
        }
    }
}
