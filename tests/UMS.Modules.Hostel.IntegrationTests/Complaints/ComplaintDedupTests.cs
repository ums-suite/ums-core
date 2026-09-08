using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Hostel.Application.Allocations;
using UMS.Modules.Hostel.Application.Complaints;
using UMS.Modules.Hostel.IntegrationTests.Infrastructure;

namespace UMS.Modules.Hostel.IntegrationTests.Complaints;

/// <summary>HOS-15: edge-cases.md "Duplicate complaint submission from a double-click or client retry" - design-decisions.md "Complaint Deduplication Mechanism".</summary>
[Collection(HostelApiTestCollectionDefinition.Name)]
public sealed class ComplaintDedupTests(HostelServiceFixture fixture)
{
    [Fact]
    public async Task Two_submissions_with_the_same_idempotency_key_return_the_same_Complaint()
    {
        var (studentId, allocationId) = await SeedActiveAllocationAsync();
        var idempotencyKey = $"key-{Guid.NewGuid():N}";

        using var scope1 = fixture.Services.CreateScope();
        var first = await scope1.ServiceProvider.GetRequiredService<ComplaintService>()
            .SubmitAsync(studentId, new SubmitComplaintRequest(allocationId, "Maintenance", "The fan is broken.", idempotencyKey));

        using var scope2 = fixture.Services.CreateScope();
        var second = await scope2.ServiceProvider.GetRequiredService<ComplaintService>()
            .SubmitAsync(studentId, new SubmitComplaintRequest(allocationId, "Maintenance", "The fan is broken.", idempotencyKey));

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.Id, second.Value.Id);
    }

    [Fact]
    public async Task Two_submissions_without_an_idempotency_key_within_the_dedupe_window_return_the_same_Complaint()
    {
        var (studentId, allocationId) = await SeedActiveAllocationAsync();

        using var scope1 = fixture.Services.CreateScope();
        var first = await scope1.ServiceProvider.GetRequiredService<ComplaintService>()
            .SubmitAsync(studentId, new SubmitComplaintRequest(allocationId, "Damage", "Broken window.", IdempotencyKey: null));

        using var scope2 = fixture.Services.CreateScope();
        var second = await scope2.ServiceProvider.GetRequiredService<ComplaintService>()
            .SubmitAsync(studentId, new SubmitComplaintRequest(allocationId, "Damage", "Broken window.", IdempotencyKey: null));

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.Id, second.Value.Id);
    }

    [Fact]
    public async Task A_Complaint_against_an_Allocation_the_Student_does_not_own_is_rejected()
    {
        var (_, allocationId) = await SeedActiveAllocationAsync();
        var someoneElseId = Guid.NewGuid();

        using var scope = fixture.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ComplaintService>()
            .SubmitAsync(someoneElseId, new SubmitComplaintRequest(allocationId, "Maintenance", "Not mine.", null));

        Assert.True(result.IsFailure);
        Assert.Equal("complaint.not_owner", result.Error!.Code);
    }

    private async Task<(Guid StudentId, Guid AllocationId)> SeedActiveAllocationAsync()
    {
        var (hostelId, _, _) = await HostelTestData.CreateHostelWithSingleBedRoomAsync(fixture.Services);
        var windowId = await HostelTestData.CreateOpenApplicationWindowAsync(fixture.Services);
        var studentId = Guid.NewGuid();
        var applicationId = await HostelTestData.SubmitAndRankApplicationAsync(fixture.Services, studentId, hostelId, windowId);

        using var scope = fixture.Services.CreateScope();
        var allocationService = scope.ServiceProvider.GetRequiredService<AllocationService>();
        var allocation = (await allocationService.ApproveAndAllocateAsync(applicationId, Guid.NewGuid(), Guid.NewGuid().ToString())).Value;
        return (studentId, allocation.Id);
    }
}
