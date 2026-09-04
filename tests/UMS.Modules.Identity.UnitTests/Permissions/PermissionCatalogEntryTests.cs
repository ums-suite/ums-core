using UMS.Modules.Identity.Domain.Permissions;

namespace UMS.Modules.Identity.UnitTests.Permissions;

public class PermissionCatalogEntryTests
{
    private static readonly DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("identity.role.assign")]
    [InlineData("academic.result.publish")]
    [InlineData("student.result.read")]
    public void Register_accepts_a_well_formed_key(string key)
    {
        var entry = PermissionCatalogEntry.Register(key, "identity", "description", _now);

        Assert.Equal(key, entry.Key);
    }

    [Theory]
    [InlineData("Identity.Role.Assign")] // must be lowercase
    [InlineData("identity.role")] // must have at least module.resource.action
    [InlineData("identity")]
    [InlineData("")]
    [InlineData("identity..assign")]
    public void Register_rejects_a_malformed_key(string key)
    {
        Assert.Throws<ArgumentException>(() => PermissionCatalogEntry.Register(key, "identity", "description", _now));
    }

    [Fact]
    public void UpdateMetadata_replaces_owner_and_description_in_place()
    {
        var entry = PermissionCatalogEntry.Register("identity.role.assign", "identity", "old description", _now);

        entry.UpdateMetadata("identity", "new description", _now.AddDays(1));

        Assert.Equal("new description", entry.Description);
    }
}
