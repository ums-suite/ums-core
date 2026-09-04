namespace UMS.Modules.Identity.Application;

/// <summary>IDN-12 tuning (bound from <c>Identity:PasswordReset</c>) - requirement-spec.md identity §2 "single-use, short-TTL".</summary>
public sealed class IdentityPasswordResetOptions
{
    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromMinutes(30);
}
