namespace UMS.Modules.Identity.Infrastructure.Security;

/// <summary>
/// Argon2id cost parameters (design-decisions.md, "Password Hashing Algorithm Choice": "cost
/// parameters tuned to a target verification time in the tens-of-milliseconds range on production
/// hardware"). Defaults are the OWASP-recommended v1 minimums; bound from
/// <c>Identity:Argon2</c> configuration so a future re-tune is a config change. Integration tests
/// override these to smaller values purely for suite runtime - never done in a real environment.
/// </summary>
public sealed class Argon2idOptions
{
    public int MemorySizeKb { get; set; } = 19_456;

    public int Iterations { get; set; } = 2;

    public int Parallelism { get; set; } = 1;
}
