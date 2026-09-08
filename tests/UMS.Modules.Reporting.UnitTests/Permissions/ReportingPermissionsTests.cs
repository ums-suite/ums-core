using System.Reflection;
using System.Text.RegularExpressions;
using UMS.Modules.Reporting.Application.Permissions;

namespace UMS.Modules.Reporting.UnitTests.Permissions;

/// <summary>ums-conventions.md's permission-string shape: 3+ dot-separated lowercase-alphanumeric segments, no underscores.</summary>
public sealed partial class ReportingPermissionsTests
{
    [Fact]
    public void Every_declared_permission_has_at_least_three_lowercase_alphanumeric_segments()
    {
        var permissions = typeof(ReportingPermissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.NotEmpty(permissions);

        foreach (var permission in permissions)
        {
            var segments = permission.Split('.');
            Assert.True(segments.Length >= 3, $"'{permission}' has fewer than 3 segments.");
            Assert.All(segments, segment => Assert.Matches(SegmentPattern(), segment));
        }
    }

    [Fact]
    public void Every_declared_permission_is_unique()
    {
        var permissions = typeof(ReportingPermissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.Equal(permissions.Count, permissions.Distinct().Count());
    }

    [GeneratedRegex("^[a-z0-9]+$")]
    private static partial Regex SegmentPattern();
}
