using UMS.Modules.Reporting.Domain.RegulatoryReports;

namespace UMS.Modules.Reporting.UnitTests.RegulatoryReports;

public sealed class RegulatoryReportDefinitionTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 0, 0, 0, TimeSpan.Zero);
    private static readonly List<(string FieldKey, string Label)> OneField = [("TotalEnrollments", "Total Enrollments")];

    [Fact]
    public void Create_rejects_a_blank_name()
    {
        var result = RegulatoryReportDefinition.Create(
            "   ",
            RegulatoryReportCategory.StudentEnrollment,
            OneField,
            "{}",
            "[]",
            RegulatoryReportFormat.Csv,
            Guid.NewGuid(),
            Now);

        Assert.True(result.IsFailure);
        Assert.Equal("regulatory_report_definition.name_required", result.Error!.Code);
    }

    [Fact]
    public void Create_rejects_zero_field_selections()
    {
        var result = RegulatoryReportDefinition.Create(
            "Student Enrollment",
            RegulatoryReportCategory.StudentEnrollment,
            [],
            "{}",
            "[]",
            RegulatoryReportFormat.Csv,
            Guid.NewGuid(),
            Now);

        Assert.True(result.IsFailure);
        Assert.Equal("regulatory_report_definition.fields_required", result.Error!.Code);
    }

    [Fact]
    public void Create_rejects_RegulatoryReportFormat_None()
    {
        var result = RegulatoryReportDefinition.Create(
            "Student Enrollment",
            RegulatoryReportCategory.StudentEnrollment,
            OneField,
            "{}",
            "[]",
            RegulatoryReportFormat.None,
            Guid.NewGuid(),
            Now);

        Assert.True(result.IsFailure);
        Assert.Equal("regulatory_report_definition.format_required", result.Error!.Code);
    }

    [Fact]
    public void Create_assigns_positional_ordinals_never_a_caller_supplied_identity()
    {
        var fields = new List<(string FieldKey, string Label)>
        {
            ("A", "Field A"),
            ("B", "Field B"),
            ("C", "Field C"),
        };

        var result = RegulatoryReportDefinition.Create(
            "Program Statistics",
            RegulatoryReportCategory.ProgramStatistics,
            fields,
            "{}",
            "[]",
            RegulatoryReportFormat.Pdf,
            Guid.NewGuid(),
            Now);

        Assert.True(result.IsSuccess);
        var ordinals = result.Value.FieldSelections.OrderBy(f => f.Ordinal).Select(f => (f.FieldKey, f.Ordinal)).ToList();
        Assert.Equal([("A", 0), ("B", 1), ("C", 2)], ordinals);
    }

    [Fact]
    public void UpdateConfiguration_replaces_field_selections_wholesale()
    {
        var definition = CreateValid();

        var updated = definition.UpdateConfiguration(
            "Student Enrollment (Revised)",
            [("X", "Field X")],
            "{}",
            "[]",
            RegulatoryReportFormat.Csv,
            Now.AddDays(1));

        Assert.True(updated.IsSuccess);
        Assert.Equal("Student Enrollment (Revised)", definition.Name);
        Assert.Single(definition.FieldSelections);
        Assert.Equal("X", definition.FieldSelections[0].FieldKey);
        Assert.Equal(Now.AddDays(1), definition.UpdatedAt);
    }

    [Fact]
    public void UpdateConfiguration_rejects_the_same_invariants_as_Create()
    {
        var definition = CreateValid();

        var updated = definition.UpdateConfiguration(string.Empty, OneField, "{}", "[]", RegulatoryReportFormat.Csv, Now);

        Assert.True(updated.IsFailure);
        Assert.Equal("regulatory_report_definition.name_required", updated.Error!.Code);
    }

    [Fact]
    public void CaptureSnapshot_is_an_independent_copy_unaffected_by_a_later_edit()
    {
        var definition = CreateValid();
        var snapshotBeforeEdit = definition.CaptureSnapshot();

        definition.UpdateConfiguration("Renamed", [("Y", "Field Y")], "{}", "[]", RegulatoryReportFormat.Csv, Now.AddDays(1));

        // design-decisions.md "RegulatoryReportDefinition Snapshot-by-Reference": a live edit never
        // retroactively alters an already-captured snapshot.
        Assert.Equal("Student Enrollment", snapshotBeforeEdit.Name);
        Assert.Single(snapshotBeforeEdit.FieldSelections);
        Assert.Equal("TotalEnrollments", snapshotBeforeEdit.FieldSelections[0].FieldKey);
        Assert.Equal("Renamed", definition.Name);
    }

    [Fact]
    public void Deactivate_flips_IsActive_and_stamps_UpdatedAt()
    {
        var definition = CreateValid();

        definition.Deactivate(Now.AddDays(2));

        Assert.False(definition.IsActive);
        Assert.Equal(Now.AddDays(2), definition.UpdatedAt);
    }

    private static RegulatoryReportDefinition CreateValid()
    {
        var result = RegulatoryReportDefinition.Create(
            "Student Enrollment",
            RegulatoryReportCategory.StudentEnrollment,
            OneField,
            "{}",
            "[\"academic-dashboard\"]",
            RegulatoryReportFormat.Pdf | RegulatoryReportFormat.Csv,
            Guid.NewGuid(),
            Now);

        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
