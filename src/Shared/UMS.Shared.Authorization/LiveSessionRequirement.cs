using Microsoft.AspNetCore.Authorization;

namespace UMS.Shared.Authorization;

/// <summary>"Authenticated, active User, non-revoked Session" - no specific Permission string required. See <see cref="IPermissionResolver.CheckLivenessAsync"/>.</summary>
public sealed class LiveSessionRequirement : IAuthorizationRequirement;
