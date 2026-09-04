using UMS.Modules.Identity.Domain.Roles;

namespace UMS.Modules.Identity.UnitTests.Roles;

public class RoleTests
{
    private static readonly DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_deduplicates_permission_keys()
    {
        var role = Role.Create("Registrar", "University-wide registrar", ["identity.user.manage", "identity.user.manage"], _now);

        Assert.Single(role.Permissions);
    }

    [Fact]
    public void Create_throws_for_a_blank_name()
    {
        Assert.Throws<ArgumentException>(() => Role.Create("   ", null, [], _now));
    }

    [Fact]
    public void SetPermissions_wholesale_replaces_the_bundle_rather_than_appending()
    {
        var role = Role.Create("Accountant", null, ["finance.payment.refund"], _now);

        role.SetPermissions(["finance.ledger.read"]);

        Assert.DoesNotContain("finance.payment.refund", role.Permissions);
        Assert.Contains("finance.ledger.read", role.Permissions);
    }

    [Fact]
    public void SetPermissions_ignores_blank_entries()
    {
        var role = Role.Create("Student", null, ["student.profile.read", "", "  "], _now);

        Assert.Single(role.Permissions);
    }
}
