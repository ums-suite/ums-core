using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Hostel.Application.Applications;
using UMS.Modules.Hostel.Application.ApplicationWindows;
using UMS.Modules.Hostel.Application.Hostels;

namespace UMS.Modules.Hostel.IntegrationTests.Infrastructure;

/// <summary>Seeds test fixtures through the real Application services (never raw SQL), mirroring Finance's/Admission's own <c>*TestData</c> helper pattern.</summary>
internal static class HostelTestData
{
    public static async Task<(Guid HostelId, Guid RoomId, Guid BedId)> CreateHostelWithSingleBedRoomAsync(IServiceProvider services, string roomType = "SingleOccupancy")
    {
        using var scope = services.CreateScope();
        var inventory = scope.ServiceProvider.GetRequiredService<InventoryService>();

        var hostel = (await inventory.CreateHostelAsync(new CreateHostelRequest($"Hostel-{Guid.NewGuid():N}", "Mixed"))).Value;
        var building = (await inventory.CreateBuildingAsync(new CreateBuildingRequest(hostel.Id, "Building-A"))).Value;
        var room = (await inventory.CreateRoomAsync(new CreateRoomRequest(building.Id, $"R-{Guid.NewGuid():N}"[..8], roomType, Capacity: 1))).Value;
        var bed = (await inventory.CreateBedAsync(new CreateBedRequest(room.Id, "Bed-1"))).Value;

        return (hostel.Id, room.Id, bed.Id);
    }

    public static async Task<Guid> CreateOpenApplicationWindowAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var windows = scope.ServiceProvider.GetRequiredService<ApplicationWindowService>();

        var now = DateTimeOffset.UtcNow;
        var window = (await windows.CreateAsync(new CreateApplicationWindowRequest($"Session-{Guid.NewGuid():N}", now.AddDays(-1), now.AddDays(30)))).Value;
        return window.Id;
    }

    /// <summary>Submits a HostelApplication for <paramref name="studentId"/> ranking a single Hostel/RoomType preference, then runs the real HOS-4 ranking pass so it ends up Ranked+eligible, ready for HOS-6/HOS-7's review-approval flow.</summary>
    public static async Task<Guid> SubmitAndRankApplicationAsync(IServiceProvider services, Guid studentId, Guid hostelId, Guid applicationWindowId, string roomType = "SingleOccupancy")
    {
        using var scope = services.CreateScope();
        var applications = scope.ServiceProvider.GetRequiredService<HostelApplicationService>();
        var ranking = scope.ServiceProvider.GetRequiredService<HostelApplicationRankingService>();

        var request = new CreateHostelApplicationRequest(
            applicationWindowId,
            YearOfStudy: 2,
            HasFinancialNeed: false,
            HomeDistrictDistanceKm: null,
            Preferences: [new HostelPreferenceDto(hostelId, roomType, Rank: 1)]);

        var application = (await applications.SubmitNewApplicationAsync(studentId, request, DateTimeOffset.UtcNow)).Value;
        await ranking.RankWindowAsync(applicationWindowId);
        return application.Id;
    }
}
