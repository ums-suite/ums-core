using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Academic.Application.Abstractions;
using UMS.Modules.Academic.Application.AcademicSessions;
using UMS.Modules.Academic.Application.Attendance;
using UMS.Modules.Academic.Application.CourseOfferings;
using UMS.Modules.Academic.Application.Courses;
using UMS.Modules.Academic.Application.Curricula;
using UMS.Modules.Academic.Application.Enrollments;
using UMS.Modules.Academic.Application.Grades;
using UMS.Modules.Academic.Application.Programs;
using UMS.Modules.Academic.Application.ResultPublications;
using UMS.Modules.Academic.Infrastructure.Authorization;
using UMS.Modules.Academic.Infrastructure.Persistence;
using UMS.Modules.Academic.Infrastructure.Persistence.Repositories;
using UMS.Shared.Authorization;

namespace UMS.Modules.Academic.Infrastructure;

/// <summary>
/// Composition root for the Academic module (release/DEVELOPMENT_PLAN.md Flow #12) - mirrors
/// Faculty/Student's own <c>DependencyInjection</c> exactly. No other module or the Host itself
/// ever references <c>UMS.Modules.Academic.Domain</c>/<c>.Infrastructure</c> directly
/// (module-boundaries.md); nothing outside this module needs to today (no
/// <c>UMS.Shared.Academic</c> library exists - see this module's own PR "Known gaps" section for
/// why that's a deliberate simplification, not an oversight).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddAcademicModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AcademicDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("Postgres")
                    ?? throw new InvalidOperationException("Missing required 'ConnectionStrings:Postgres' configuration value."),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "academic")));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AcademicDbContext>());
        services.AddScoped<IDomainEventRecorder>(sp => sp.GetRequiredService<AcademicDbContext>());

        services.AddScoped<IProgramRepository, ProgramRepository>();
        services.AddScoped<ICourseRepository, CourseRepository>();
        services.AddScoped<ICurriculumRepository, CurriculumRepository>();
        services.AddScoped<IAcademicSessionRepository, AcademicSessionRepository>();
        services.AddScoped<ICourseOfferingRepository, CourseOfferingRepository>();
        services.AddScoped<IEnrollmentRepository, EnrollmentRepository>();
        services.AddScoped<IResultPublicationRepository, ResultPublicationRepository>();
        services.AddScoped<IAttendanceSessionRepository, AttendanceSessionRepository>();
        services.AddScoped<IOutboxReader, OutboxReader>();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPermissionManifest, AcademicPermissionManifest>();

        // Cross-module dependencies (module-boundaries.md: Academic depends on Identity,
        // Organization, Student, Faculty) are all resolved directly against their real
        // implementations, already registered by the Host before AddAcademicModule runs -
        // Organization (Flow #6) via UMS.Shared.Organization.IOrganizationNodeExistenceChecker,
        // Faculty (Flow #10) via UMS.Shared.Faculty.IFacultyMemberLookup, Student (Flow #11) via
        // UMS.Shared.Student.IStudentStatusChecker (added by THIS build - see that interface's own
        // remarks). Academic is the last-built module with no downstream stub-then-promote gap of
        // its own to close.
        // release/DEVELOPMENT_PLAN.md Flow #13 (Learning, LRN-1) - Academic's first OUTWARD-facing
        // read contract. Learning resolves CourseOffering existence, Instructor identity, and
        // enrollment membership exclusively through this, never against Faculty/Student directly
        // (learning requirement-spec.md §9.1) and never a direct schema join (ADR-0002).
        services.AddScoped<UMS.Shared.Academic.ICourseOfferingLookup, CrossModule.CourseOfferingLookupAdapter>();

        // RPT-3 (release/DEVELOPMENT_PLAN.md Flow #22, Reporting): Academic's own outward-facing
        // reporting-query contract - see CrossModule.AcademicReportingQueryAdapter's own remarks.
        // No consumer exists yet in THIS module's own build order (Reporting/Flow #22 is registered
        // after Academic in Host/Workers) - same "contract ships before its first real caller"
        // posture ICourseOfferingLookup itself once had for Learning.
        services.AddScoped<UMS.Shared.Academic.IAcademicReportingQuery, CrossModule.AcademicReportingQueryAdapter>();

        // release/DEVELOPMENT_PLAN.md Flow #16 (Student - Admission Integration, STU-10) - Student's
        // transcript-request StudentRequest resolves this in-process instead of calling Academic's
        // own GET /students/{id}/transcript endpoint over HTTP (ADR-0003).
        services.AddScoped<UMS.Shared.Academic.ITranscriptQuery, CrossModule.TranscriptQueryAdapter>();

        services.AddScoped<ProgramService>();
        services.AddScoped<CourseService>();
        services.AddScoped<CurriculumService>();
        services.AddScoped<AcademicSessionService>();
        services.AddScoped<CourseOfferingService>();
        services.AddScoped<CourseOfferingQueryService>();
        services.AddScoped<AssessmentService>();
        services.AddScoped<EnrollmentService>();
        services.AddScoped<AttendanceService>();
        services.AddScoped<GradeService>();
        services.AddScoped<GradeCorrectionService>();
        services.AddScoped<ResultPublicationService>();
        services.AddScoped<StudentResultQueryService>();

        return services;
    }

    /// <summary>Applies pending EF Core migrations for the <c>academic</c> schema - called once from the Host composition root, mirroring Faculty/Student's own <c>UseXModuleAsync</c>.</summary>
    public static async Task UseAcademicModuleAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AcademicDbContext>();
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);
    }
}
