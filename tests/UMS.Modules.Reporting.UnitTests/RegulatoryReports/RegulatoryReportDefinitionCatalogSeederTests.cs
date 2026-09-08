using UMS.Modules.Reporting.Application.RegulatoryReports;
using UMS.Modules.Reporting.Domain.RegulatoryReports;

namespace UMS.Modules.Reporting.UnitTests.RegulatoryReports;

/// <summary>
/// Flow #26: verifies the seeder's "Research" category was actually fixed (no longer a Faculty-
/// headcount proxy) and that the four OTHER documented proxies (Gender Distribution/Infrastructure/
/// Scholarships/International Students) were deliberately left untouched - out of this flow's scope.
/// </summary>
public sealed class RegulatoryReportDefinitionCatalogSeederTests
{
    private static RegulatoryReportDefinitionSeed GetByName(string name) =>
        Assert.Single(RegulatoryReportDefinitionCatalogSeeder.Catalog, s => s.Name == name);

    [Fact]
    public void The_Research_category_now_sources_the_real_research_dashboard_metric()
    {
        var research = GetByName("Research");

        Assert.Equal(RegulatoryReportCategory.Research, research.Category);
        Assert.Equal(["research-dashboard"], research.SourceQueryReferences);
        Assert.DoesNotContain("faculty-dashboard", research.SourceQueryReferences);

        var fieldKeys = research.Fields.Select(f => f.FieldKey).ToList();
        Assert.Contains("TotalActiveGrants", fieldKeys);
        Assert.Contains("ConfirmedFundingAmountByCurrency", fieldKeys);
        Assert.Contains("TotalPublications", fieldKeys);
        Assert.Contains("TotalRepositoryEntries", fieldKeys);
        Assert.DoesNotContain("TotalFacultyMembers", fieldKeys);
    }

    [Theory]
    [InlineData("Gender Distribution", "academic-dashboard")]
    [InlineData("Infrastructure", "hostel-dashboard")]
    [InlineData("Scholarships", "financial-dashboard")]
    [InlineData("International Students", "academic-dashboard")]
    public void The_four_other_documented_proxies_remain_deliberately_untouched(string categoryName, string expectedSourceQueryReference)
    {
        var seed = GetByName(categoryName);

        Assert.Equal([expectedSourceQueryReference], seed.SourceQueryReferences);

        // Still an honestly-labeled proxy, not a genuine dedicated field - a future,
        // Student/Organization/Finance-dependent top-up, not this flow, would replace these.
        Assert.Contains(seed.Fields, field => field.Label.Contains("pending", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_catalog_still_seeds_exactly_eleven_categories()
    {
        Assert.Equal(11, RegulatoryReportDefinitionCatalogSeeder.Catalog.Count);
        Assert.Equal(11, RegulatoryReportDefinitionCatalogSeeder.Catalog.Select(s => s.Category).Distinct().Count());
    }
}
