using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;
using UMS.Modules.Documents.Application.Abstractions;
using UMS.Modules.Documents.Application.BulkJobs;
using UMS.Modules.Documents.Application.Generation;
using UMS.Modules.Documents.Application.Templates;
using UMS.Modules.Documents.Application.Uploads;
using UMS.Modules.Documents.Application.Verification;
using UMS.Modules.Documents.Infrastructure.Authorization;
using UMS.Modules.Documents.Infrastructure.Notifications;
using UMS.Modules.Documents.Infrastructure.Persistence;
using UMS.Modules.Documents.Infrastructure.Persistence.Repositories;
using UMS.Modules.Documents.Infrastructure.Rendering;
using UMS.Modules.Documents.Infrastructure.Storage;
using UMS.Modules.Documents.Infrastructure.Verification;
using UMS.Shared.Audit;
using UMS.Shared.Authorization;

namespace UMS.Modules.Documents.Infrastructure;

/// <summary>Composition root for the Documents module - mirrors Audit's own <c>DependencyInjection</c> exactly.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddDocumentsModule(this IServiceCollection services, IConfiguration configuration)
    {
        // QuestPDF requires a one-time, process-wide license declaration (see
        // Rendering.DocumentPdfRenderer's own remarks for the license reasoning) - Community is
        // free for this project's likely operating profile, but see that class's caveat.
        QuestPDF.Settings.License = LicenseType.Community;

        services.AddDbContext<DocumentsDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("Postgres")
                    ?? throw new InvalidOperationException("Missing required 'ConnectionStrings:Postgres' configuration value."),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "documents")));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<DocumentsDbContext>());
        services.AddScoped<IOutboxEnqueuer>(sp => sp.GetRequiredService<DocumentsDbContext>());
        services.AddScoped<IOutboxReader, OutboxReader>();

        services.AddScoped<IDocumentTemplateRepository, DocumentTemplateRepository>();
        services.AddScoped<IGeneratedDocumentRepository, GeneratedDocumentRepository>();
        services.AddScoped<IBulkGenerationJobRepository, BulkGenerationJobRepository>();
        services.AddScoped<IBulkGenerationJobItemRepository, BulkGenerationJobItemRepository>();
        services.AddScoped<IUploadedArtifactRepository, UploadedArtifactRepository>();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IVerificationIdGenerator, VerificationIdGenerator>();

        services.Configure<ObjectStorageOptions>(configuration.GetSection("Documents:Storage"));
        services.AddSingleton<IObjectStorage, S3ObjectStorage>();

        services.Configure<DocumentRenderingOptions>(configuration.GetSection("Documents:Rendering"));
        services.AddSingleton<IDocumentRenderer, DocumentPdfRenderer>();

        // DOC-13: Notifications (Flow #8) now exists - resolves the real cross-module intake
        // adapter (NotificationRequestIntakeAdapter) in place of the former
        // StubNotificationRequestPublisher, the same in-process shared-interface pattern
        // UMS.Shared.Audit/UMS.Shared.Organization already established. Scoped, not singleton
        // (the former stub's lifetime) - the real INotificationRequestIntake implementation
        // consumes a scoped NotificationsDbContext underneath.
        services.AddScoped<INotificationRequestPublisher, NotificationRequestIntakeAdapter>();

        // release/DEVELOPMENT_PLAN.md Flow #11 (Student, STU-3) - the cross-module inbound path
        // Student resolves to request the student ID card as a side effect of CreateStudentRecord.
        services.AddScoped<UMS.Shared.Documents.IDocumentGenerationRequester, UMS.Modules.Documents.Infrastructure.Student.DocumentGenerationRequesterAdapter>();

        services.AddScoped<GeneratedDocumentPipeline>();
        services.AddScoped<DocumentTemplateService>();
        services.AddScoped<GenerateDocumentService>();
        services.AddScoped<GetDocumentService>();
        services.AddScoped<ListDocumentsService>();
        services.AddScoped<RevokeDocumentService>();
        services.AddScoped<VerifyDocumentService>();
        services.AddScoped<RequestBulkGenerationService>();
        services.AddScoped<BulkJobStatusService>();
        services.AddScoped<UploadedArtifactService>();
        services.AddScoped<PendingDocumentSweepService>();

        services.AddSingleton<IPermissionManifest, DocumentPermissionManifest>();

        return services;
    }

    /// <summary>
    /// Applies pending EF Core migrations for the <c>documents</c> schema and ensures the
    /// configured object-storage bucket exists - called once from the Host/Workers composition
    /// root, mirroring Audit's own <c>UseAuditModuleAsync</c>. The bucket-ensure step is new
    /// relative to Audit's own version - see <c>S3ObjectStorage.EnsureBucketExistsAsync</c>'s
    /// remarks for why.
    /// </summary>
    public static async Task UseDocumentsModuleAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);

        if (scope.ServiceProvider.GetRequiredService<IObjectStorage>() is S3ObjectStorage s3ObjectStorage)
        {
            await s3ObjectStorage.EnsureBucketExistsAsync().ConfigureAwait(false);
        }
    }
}
