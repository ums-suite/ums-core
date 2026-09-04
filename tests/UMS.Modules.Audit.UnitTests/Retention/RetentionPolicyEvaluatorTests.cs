using Microsoft.Extensions.Options;
using UMS.Modules.Audit.Application.Retention;

namespace UMS.Modules.Audit.UnitTests.Retention;

public class RetentionPolicyEvaluatorTests
{
    private static RetentionPolicyEvaluator CreateEvaluator(Dictionary<string, int> entityTypeRetentionMonths)
    {
        var options = Options.Create(new AuditRetentionOptions { EntityTypeRetentionMonths = entityTypeRetentionMonths });
        return new RetentionPolicyEvaluator(options);
    }

    [Fact]
    public void IsPartitionArchivable_is_false_for_an_entity_type_with_no_configured_retention()
    {
        var evaluator = CreateEvaluator([]);
        var partitionEnd = DateTimeOffset.Parse("2020-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

        var archivable = evaluator.IsPartitionArchivable(partitionEnd, ["Grade"], DateTimeOffset.UtcNow);

        Assert.False(archivable);
    }

    [Fact]
    public void IsPartitionArchivable_is_true_once_the_configured_window_has_elapsed()
    {
        var evaluator = CreateEvaluator(new Dictionary<string, int> { ["Notice"] = 6 });
        var partitionEnd = DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var asOf = partitionEnd.AddMonths(7);

        var archivable = evaluator.IsPartitionArchivable(partitionEnd, ["Notice"], asOf);

        Assert.True(archivable);
    }

    [Fact]
    public void IsPartitionArchivable_is_false_before_the_configured_window_has_elapsed()
    {
        var evaluator = CreateEvaluator(new Dictionary<string, int> { ["Notice"] = 6 });
        var partitionEnd = DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var asOf = partitionEnd.AddMonths(3);

        var archivable = evaluator.IsPartitionArchivable(partitionEnd, ["Notice"], asOf);

        Assert.False(archivable);
    }

    [Fact]
    public void IsPartitionArchivable_is_false_when_a_partition_mixes_an_indefinite_and_a_configured_entity_type()
    {
        var evaluator = CreateEvaluator(new Dictionary<string, int> { ["Notice"] = 1 });
        var partitionEnd = DateTimeOffset.Parse("2020-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var asOf = DateTimeOffset.UtcNow;

        // "Grade" has no configured window (indefinite) even though "Notice" has long expired -
        // the whole partition must be kept per design-decisions.md's "a partition holding even
        // one indefinite-retention entity type is never archived away" rule.
        var archivable = evaluator.IsPartitionArchivable(partitionEnd, ["Notice", "Grade"], asOf);

        Assert.False(archivable);
    }

    [Fact]
    public void IsPartitionArchivable_is_true_for_an_empty_partition()
    {
        var evaluator = CreateEvaluator([]);

        var archivable = evaluator.IsPartitionArchivable(DateTimeOffset.UtcNow, [], DateTimeOffset.UtcNow);

        Assert.True(archivable);
    }
}
