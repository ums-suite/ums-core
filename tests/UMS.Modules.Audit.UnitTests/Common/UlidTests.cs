using UMS.Modules.Audit.Domain.Common;

namespace UMS.Modules.Audit.UnitTests.Common;

public class UlidTests
{
    [Fact]
    public void NewUlid_produces_a_26_character_value()
    {
        var value = Ulid.NewUlid();

        Assert.Equal(26, value.Length);
    }

    [Fact]
    public void NewUlid_produces_strictly_increasing_values_for_sequential_calls()
    {
        var previous = Ulid.NewUlid();

        for (var i = 0; i < 1000; i++)
        {
            var next = Ulid.NewUlid();
            Assert.True(string.CompareOrdinal(next, previous) > 0, $"'{next}' did not sort after '{previous}'.");
            previous = next;
        }
    }

    [Fact]
    public void NewUlid_uses_only_crockford_base32_characters()
    {
        var value = Ulid.NewUlid();

        Assert.All(value, c => Assert.Contains(c, "0123456789ABCDEFGHJKMNPQRSTVWXYZ"));
    }
}
