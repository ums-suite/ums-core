using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace UMS.Shared.Authorization;

/// <summary>
/// Builds a <see cref="PermissionRequirement"/> policy on the fly for any policy name of the form
/// <c>perm:&lt;permission-string&gt;</c>, so a module never has to pre-register one named policy
/// per permission string in the manifest - it just calls
/// <see cref="DependencyInjection.RequirePermission(Microsoft.AspNetCore.Builder.RouteHandlerBuilder, string)"/>
/// with the permission string itself. Falls back to <see cref="DefaultAuthorizationPolicyProvider"/>
/// for every other policy name (e.g. plain <c>RequireAuthorization()</c> with no policy name).
/// </summary>
public sealed class DynamicPermissionPolicyProvider(IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
{
    public const string PermissionPolicyPrefix = "perm:";

    private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(PermissionPolicyPrefix, StringComparison.Ordinal))
        {
            return _fallback.GetPolicyAsync(policyName);
        }

        var permission = policyName[PermissionPolicyPrefix.Length..];
        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permission))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}
