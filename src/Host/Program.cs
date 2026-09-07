using HealthChecks.NpgSql;
using HealthChecks.Redis;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using StackExchange.Redis;
using UMS.Modules.Academic.Api;
using UMS.Modules.Academic.Infrastructure;
using UMS.Modules.Audit.Api;
using UMS.Modules.Audit.Infrastructure;
using UMS.Modules.Documents.Api;
using UMS.Modules.Documents.Infrastructure;
using UMS.Modules.Faculty.Api;
using UMS.Modules.Faculty.Infrastructure;
using UMS.Modules.Identity.Api;
using UMS.Modules.Identity.Infrastructure;
using UMS.Modules.Learning.Api;
using UMS.Modules.Learning.Infrastructure;
using UMS.Modules.Notifications.Api;
using UMS.Modules.Notifications.Infrastructure;
using UMS.Modules.Organization.Api;
using UMS.Modules.Organization.Infrastructure;
using UMS.Modules.Student.Api;
using UMS.Modules.Student.Infrastructure;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.Observability;
using UMS.Shared.Resilience;
using UMS.Shared.Resilience.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.AddUmsObservability(serviceName: "ums-core");

builder.Services.AddUmsErrorHandling();
await builder.Services.AddUmsResilienceAsync(builder.Configuration);

// Identity (release/DEVELOPMENT_PLAN.md Flow #4) is the first business module - every later
// module adds its own AddXModule(builder.Configuration) call here the same way.
builder.Services.AddIdentityModule(builder.Configuration);

// Audit (release/DEVELOPMENT_PLAN.md Flow #5) - depends on no other module (module-boundaries.md);
// every other module resolves its cross-module write path via UMS.Shared.Audit.IAuditRecorder,
// registered here.
builder.Services.AddAuditModule(builder.Configuration);

// Organization (release/DEVELOPMENT_PLAN.md Flow #6) - depends only on Identity for its own
// endpoint authz (module-boundaries.md); other modules resolve its cross-module read path via
// UMS.Shared.Organization.IOrganizationNodeExistenceChecker, registered here (Identity's own
// former stub registration now resolves this instead - see Identity.Infrastructure's
// DependencyInjection.cs).
builder.Services.AddOrganizationModule(builder.Configuration);

// Documents (release/DEVELOPMENT_PLAN.md Flow #9) - depends only on Identity (module-boundaries.md);
// calls back into Audit's IAuditRecorder (DOC-14, already registered above) for official-record
// document types.
builder.Services.AddDocumentsModule(builder.Configuration);

// Notifications (release/DEVELOPMENT_PLAN.md Flow #8) - depends only on Identity
// (module-boundaries.md): resolves UMS.Shared.Identity.IRecipientDirectory (registered above by
// AddIdentityModule) for recipient contact-info/language lookup (NTF-2), and
// UMS.Shared.Audit.IAuditRecorder (registered above by AddAuditModule) for NTF-17's audit
// integration. Documents (DOC-13, above) resolves its own
// UMS.Shared.Notifications.INotificationRequestIntake cross-module call against this module's real
// implementation, registered here.
builder.Services.AddNotificationsModule(builder.Configuration);

// Faculty (release/DEVELOPMENT_PLAN.md Flow #10) - depends on Identity + Organization
// (module-boundaries.md): registered after both, resolving
// UMS.Shared.Organization.IOrganizationNodeExistenceChecker (registered above) for Department
// reference validation and UMS.Shared.Notifications.INotificationRequestIntake (registered above)
// for LeaveApproved/LeaveRejected fan-out. Organization's own former StubFacultyEmploymentChecker
// registration now resolves UMS.Shared.Faculty.IFacultyEmploymentChecker instead - see
// Organization.Infrastructure's own DependencyInjection.cs.
builder.Services.AddFacultyModule(builder.Configuration);

// Student — Core (release/DEVELOPMENT_PLAN.md Flow #11) - depends on Identity, Organization,
// Documents, and Notifications (module-boundaries.md), all registered above: resolves
// UMS.Shared.Identity.IUserProvisioner (STU-2), UMS.Shared.Documents.IDocumentGenerationRequester
// (STU-3), UMS.Shared.Notifications.INotificationRequestIntake (STU-4), and
// UMS.Shared.Organization.IOrganizationNodeExistenceChecker (Department reference validation).
// Academic (Flow #12) does not exist yet, so the Program existence check is a permissive stub -
// see StudentModule's own StubProgramExistenceChecker remarks. NOTE: this stub is intentionally
// NOT promoted by Academic's own build below - module-boundaries.md's dependency table states
// Student depends on Identity/Organization/Admission only, never Academic; promoting this stub
// would create an Academic->Student->Academic cycle ADR-0002 forbids (see
// UMS.Shared.Student.IStudentStatusChecker's own remarks for the full reasoning).
builder.Services.AddStudentModule(builder.Configuration);

// Academic (release/DEVELOPMENT_PLAN.md Flow #12) - depends on Identity, Organization, Student,
// and Faculty (module-boundaries.md), all registered above: resolves
// UMS.Shared.Organization.IOrganizationNodeExistenceChecker (Department reference validation),
// UMS.Shared.Faculty.IFacultyMemberLookup (instructor eligibility/attendance-permission gate), and
// UMS.Shared.Student.IStudentStatusChecker (Enrollment's Active-status gate - a new contract this
// build adds to Student's own Infrastructure layer). Registered last since nothing else in this
// build depends on Academic.
builder.Services.AddAcademicModule(builder.Configuration);

// Learning (release/DEVELOPMENT_PLAN.md Flow #13) - depends on Identity, Organization, Academic,
// Documents, and Notifications (module-boundaries.md), all registered above, so it must come after
// Academic: it resolves UMS.Shared.Academic.ICourseOfferingLookup (CourseOffering existence,
// Instructor identity, and enrollment membership - a new contract Academic's own Infrastructure
// registers as part of this build), UMS.Shared.Documents.IUploadedArtifactRequester (LRN-5/LRN-13's
// presigned Submission/LectureMaterial uploads - also new in this build),
// UMS.Shared.Notifications.INotificationRequestIntake, and UMS.Shared.Audit.IAuditRecorder.
// Registered last, as a leaf of the dependency graph - nothing else in this build depends on
// Learning. Deliberately absent: any Academic-facing WRITE dependency (learning
// design-decisions.md's "Cross-Module Feed of Assignment Scores into Academic's Grade" -
// SubmissionEvaluated is a fan-out event only, which is what keeps the graph acyclic).
builder.Services.AddLearningModule(builder.Configuration);

// Shared JWT authentication + permission-based authorization (ums-conventions.md: one shared
// implementation, not per-module reinvention) - every module's protected endpoints gate through
// this, never their own hand-rolled [Authorize] policy.
builder.Services.AddUmsAuthentication(builder.Configuration);
builder.Services.AddUmsAuthorization();

builder.Services.AddOpenApi();

// DOC-9: the public verify endpoint is rate-limited per ums-requirements.md §11 - keyed by client
// IP via a Redis-backed fixed window (ums-conventions.md, Resilience & Reliability: "Redis-backed
// specifically because ums-core runs N replicas"), registered once here rather than per-module,
// since ASP.NET Core's rate-limiter middleware/policy registry is itself a single, app-wide
// composition-root concern (mirrors how authentication/authorization are registered once above).
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("document-verify", (HttpContext httpContext) =>
    {
        var redis = httpContext.RequestServices.GetRequiredService<IConnectionMultiplexer>();
        var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return UmsRedisRateLimiterFactory.CreatePartition(redis, "document-verify", partitionKey, permitLimit: 30, window: TimeSpan.FromMinutes(1));
    });

    // IDN-17/requirement-spec.md identity §2/§5: "rate limiting on the login and OTP endpoints
    // specifically" - the per-source-IP dimension design-decisions.md's "Rate-Limiting / Lockout
    // Mechanism" names alongside the per-identifier lockout counter Identity's own
    // IFailedLoginAttemptTracker tracks. Keyed by client IP, same Redis-backed fixed-window
    // mechanism as document-verify above. Deliberately generous (this coarse per-IP layer exists
    // to blunt a distributed, many-accounts-from-one-source attack per design-decisions.md's own
    // framing - genuine credential stuffing runs at a far higher volume than these limits - not to
    // throttle ordinary traffic from a shared NAT/campus IP, which login sees constantly).
    AddIdentityIpRateLimitPolicy(options, "identity-login", permitLimit: 100, window: TimeSpan.FromMinutes(1));
    AddIdentityIpRateLimitPolicy(options, "identity-mfa-verify", permitLimit: 60, window: TimeSpan.FromMinutes(1));
    AddIdentityIpRateLimitPolicy(options, "identity-password-forgot", permitLimit: 20, window: TimeSpan.FromMinutes(15));
    AddIdentityIpRateLimitPolicy(options, "identity-password-reset", permitLimit: 20, window: TimeSpan.FromMinutes(15));
});

// Postgres readiness check today verifies raw connectivity only - each module adds its own
// dependency-specific readiness check (ums-conventions.md, Observability) once it exists and owns
// a schema/DbContext to check against.
builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Missing required 'ConnectionStrings:Postgres' configuration value."),
        name: "postgres",
        tags: ["ready"])
    .AddRedis(
        builder.Configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Missing required 'ConnectionStrings:Redis' configuration value."),
        name: "redis",
        tags: ["ready"]);

var app = builder.Build();

// Applies each module's pending EF Core migrations and syncs the Permission catalog - see
// AddIdentityModule's own UseIdentityModuleAsync remarks. Every later module adds its own
// await line here the same way.
await app.Services.UseIdentityModuleAsync();
await app.Services.UseAuditModuleAsync();
await app.Services.UseOrganizationModuleAsync();
await app.Services.UseDocumentsModuleAsync();
await app.Services.UseNotificationsModuleAsync();
await app.Services.UseFacultyModuleAsync();
await app.Services.UseStudentModuleAsync();
await app.Services.UseAcademicModuleAsync();
await app.Services.UseLearningModuleAsync();

app.UseUmsObservability();
app.UseUmsErrorHandling();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// Liveness: process is up, no dependency checks - governs whether Kubernetes restarts a wedged
// pod. Readiness: Postgres + Redis reachable - governs whether Kubernetes routes traffic to this
// pod (ums-conventions.md, Observability).
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

// src/UMS.Modules/<Module> endpoints register themselves under /api/v1/<module>/..., starting
// with Identity (release/DEVELOPMENT_PLAN.md Flow #4).
app.MapIdentityModule();
app.MapAuditModule();
app.MapOrganizationModule();
app.MapDocumentsModule();
app.MapNotificationsModule();
app.MapFacultyModule();
app.MapStudentModule();
app.MapAcademicModule();
app.MapLearningModule();

app.Run();

/// <summary>Per-source-IP fixed-window policy, identical shape to the document-verify policy above - factored out since Identity registers four of these.</summary>
static void AddIdentityIpRateLimitPolicy(Microsoft.AspNetCore.RateLimiting.RateLimiterOptions options, string policyName, int permitLimit, TimeSpan window)
{
    options.AddPolicy(policyName, (HttpContext httpContext) =>
    {
        var redis = httpContext.RequestServices.GetRequiredService<IConnectionMultiplexer>();
        var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return UmsRedisRateLimiterFactory.CreatePartition(redis, policyName, partitionKey, permitLimit, window);
    });
}

/// <summary>Entry point type, exposed for WebApplicationFactory-based integration tests.</summary>
public partial class Program;
