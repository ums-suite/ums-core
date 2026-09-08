using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Reporting.Application.Common;
using UMS.Modules.Reporting.Application.RegulatoryReports;
using UMS.Modules.Reporting.Domain.RegulatoryReports;
using UMS.Modules.Reporting.IntegrationTests.Infrastructure;

namespace UMS.Modules.Reporting.IntegrationTests.RegulatoryReports;

/// <summary>RPT-11/RPT-16: definition CRUD against a real Postgres row, including its synchronous Audit coupling (ADR-0012) - the transactional path <c>FakeUnitOfWork</c> cannot exercise.</summary>
[Collection(ReportingTestCollectionDefinition.Name)]
public sealed class RegulatoryReportDefinitionServiceTests(ReportingServiceFixture fixture)
{
    private static AuditContext NewAuditContext() => new(Guid.NewGuid(), "127.0.0.1", Guid.NewGuid().ToString());

    [Fact]
    public async Task Creating_a_definition_persists_it_and_synchronously_records_an_Audit_entry()
    {
        using var scope = fixture.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<RegulatoryReportDefinitionService>();
        var name = $"Test Report {Guid.NewGuid():N}";

        var result = await service.CreateAsync(
            new CreateRegulatoryReportDefinitionRequest(
                name,
                RegulatoryReportCategory.StudentEnrollment,
                [new FieldSelectionRequest("TotalEnrollments", "Total Enrollments")],
                "{}",
                "[\"academic-dashboard\"]",
                RegulatoryReportFormat.Csv),
            NewAuditContext());

        Assert.True(result.IsSuccess);
        Assert.Equal(name, result.Value.Name);
        Assert.Contains(fixture.AuditRecorder.Recorded, r => r.EntityType == "RegulatoryReportDefinition" && r.Action == "Created");

        var fetched = await service.GetByIdAsync(result.Value.Id);
        Assert.True(fetched.IsSuccess);
        Assert.Equal(name, fetched.Value.Name);
    }

    [Fact]
    public async Task Creating_a_definition_with_a_duplicate_name_is_a_conflict()
    {
        using var scope = fixture.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<RegulatoryReportDefinitionService>();
        var name = $"Duplicate {Guid.NewGuid():N}";
        var request = new CreateRegulatoryReportDefinitionRequest(
            name,
            RegulatoryReportCategory.StudentEnrollment,
            [new FieldSelectionRequest("TotalEnrollments", "Total Enrollments")],
            "{}",
            "[]",
            RegulatoryReportFormat.Csv);

        var first = await service.CreateAsync(request, NewAuditContext());
        Assert.True(first.IsSuccess);

        var second = await service.CreateAsync(request, NewAuditContext());

        Assert.True(second.IsFailure);
        Assert.Equal("regulatory_report_definition.duplicate_name", second.Error!.Code);
    }

    [Fact]
    public async Task Updating_a_definition_persists_the_change_and_records_a_before_after_Audit_entry()
    {
        using var scope = fixture.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<RegulatoryReportDefinitionService>();
        var created = await service.CreateAsync(
            new CreateRegulatoryReportDefinitionRequest(
                $"Before {Guid.NewGuid():N}",
                RegulatoryReportCategory.StudentEnrollment,
                [new FieldSelectionRequest("TotalEnrollments", "Total Enrollments")],
                "{}",
                "[]",
                RegulatoryReportFormat.Csv),
            NewAuditContext());
        fixture.AuditRecorder.Recorded.Clear();

        var updated = await service.UpdateAsync(
            created.Value.Id,
            new UpdateRegulatoryReportDefinitionRequest(
                $"After {Guid.NewGuid():N}",
                [new FieldSelectionRequest("ActiveEnrollments", "Active Enrollments")],
                "{}",
                "[]",
                RegulatoryReportFormat.Pdf),
            NewAuditContext());

        Assert.True(updated.IsSuccess);
        Assert.StartsWith("After", updated.Value.Name);
        var auditEntry = Assert.Single(fixture.AuditRecorder.Recorded);
        Assert.Equal("Updated", auditEntry.Action);
        Assert.NotNull(auditEntry.BeforeValueJson);
        Assert.Contains("Before", auditEntry.BeforeValueJson);
    }

    [Fact]
    public async Task GetById_for_an_unknown_id_is_NotFound()
    {
        using var scope = fixture.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<RegulatoryReportDefinitionService>();

        var result = await service.GetByIdAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("regulatory_report_definition.not_found", result.Error!.Code);
    }
}
