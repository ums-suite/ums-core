using UMS.Modules.Organization.Application.Campuses;
using UMS.Modules.Organization.Application.Common;
using UMS.Modules.Organization.Domain.Universities;
using UMS.Modules.Organization.UnitTests.TestDoubles;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Organization.UnitTests.Campuses;

/// <summary>requirement-spec.md organization §4 "No orphan node": a child create must validate its immediate parent's existence and active status before insert.</summary>
public class CampusServiceTests
{
    private static readonly AuditContext _audit = new(Guid.NewGuid(), "127.0.0.1", "test-correlation");

    [Fact]
    public async Task CreateAsync_rejects_a_Campus_under_an_inactive_University()
    {
        var universities = new FakeUniversityRepository();
        var university = University.Create("Example University", null, DateTimeOffset.UtcNow);
        university.Deactivate();
        universities.Seed(university);

        var service = new CampusService(
            new FakeCampusRepository(),
            universities,
            new FakeFacultyRepository(),
            new FakeUnitOfWork(),
            new FakeAuditRecorder(),
            new FakeClock());

        var result = await service.CreateAsync(new CreateCampusRequest(university.Id.Value, "Main Campus"), _audit);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("university.inactive", result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_rejects_a_Campus_under_a_nonexistent_University()
    {
        var service = new CampusService(
            new FakeCampusRepository(),
            new FakeUniversityRepository(),
            new FakeFacultyRepository(),
            new FakeUnitOfWork(),
            new FakeAuditRecorder(),
            new FakeClock());

        var result = await service.CreateAsync(new CreateCampusRequest(Guid.NewGuid(), "Main Campus"), _audit);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task CreateAsync_succeeds_under_an_active_University_and_records_an_audit_entry()
    {
        var universities = new FakeUniversityRepository();
        var university = University.Create("Example University", null, DateTimeOffset.UtcNow);
        universities.Seed(university);
        var auditRecorder = new FakeAuditRecorder();

        var service = new CampusService(
            new FakeCampusRepository(),
            universities,
            new FakeFacultyRepository(),
            new FakeUnitOfWork(),
            auditRecorder,
            new FakeClock());

        var result = await service.CreateAsync(new CreateCampusRequest(university.Id.Value, "Main Campus"), _audit);

        Assert.True(result.IsSuccess);
        Assert.Equal("Main Campus", result.Value.Name);
        var recorded = Assert.Single(auditRecorder.RecordedEntries);
        Assert.Equal("Campus", recorded.EntityType);
        Assert.Equal("create", recorded.Action);
    }
}
