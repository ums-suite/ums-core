using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Library.Application.Abstractions;
using UMS.Modules.Library.Application.Catalog;
using UMS.Modules.Library.Application.Common;
using UMS.Modules.Library.Application.Fines;
using UMS.Modules.Library.Application.Loans;
using UMS.Modules.Library.Application.Reservations;
using UMS.Modules.Library.Infrastructure.Adapters;
using UMS.Modules.Library.Infrastructure.Authorization;
using UMS.Modules.Library.Infrastructure.CrossModule;
using UMS.Modules.Library.Infrastructure.Persistence;
using UMS.Modules.Library.Infrastructure.Persistence.Repositories;
using UMS.Shared.Authorization;

namespace UMS.Modules.Library.Infrastructure;

/// <summary>
/// Composition root for the Library module (Flow #20).
///
/// <para>
/// Cross-module dependencies (module-boundaries.md: Library depends on Identity, Student, Faculty,
/// Finance) resolve against their real implementations, already registered by the Host/Workers
/// composition root before <c>AddLibraryModule</c> runs - <c>UMS.Shared.Student.IStudentStatusChecker</c>,
/// <c>UMS.Shared.Faculty.IFacultyMemberLookup</c>, <c>UMS.Shared.Finance.IInvoiceRequester</c>, and
/// <c>UMS.Shared.Notifications.INotificationRequestIntake</c> are all consumed directly (or, for
/// Notifications, through this module's own <see cref="INotificationRequestPublisher"/> port).
/// </para>
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddLibraryModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<LibraryDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("Postgres")
                    ?? throw new InvalidOperationException("Missing required 'ConnectionStrings:Postgres' configuration value."),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "library")));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<LibraryDbContext>());
        services.AddScoped<IDomainEventRecorder>(sp => sp.GetRequiredService<LibraryDbContext>());

        services.AddScoped<IBookRepository, BookRepository>();
        services.AddScoped<IAuthorRepository, AuthorRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IBookCopyRepository, BookCopyRepository>();
        services.AddScoped<ILoanRepository, LoanRepository>();
        services.AddScoped<IReservationRepository, ReservationRepository>();
        services.AddScoped<IFineRepository, FineRepository>();
        services.AddScoped<IFineAccrualRepository, FineAccrualRepository>();
        services.AddScoped<ILoanReviewFlagRepository, LoanReviewFlagRepository>();
        services.AddScoped<IBookReservationSupplyFlagRepository, BookReservationSupplyFlagRepository>();
        services.AddScoped<IOverdueNoticeRepository, OverdueNoticeRepository>();
        services.AddScoped<IOutboxReader, OutboxReader>();

        services.AddScoped<IFinancePaymentEventSource, FinanceOutboxEventSource>();
        services.AddScoped<IStudentStatusEventSource, StudentOutboxEventSource>();
        services.AddScoped<IFacultyStatusEventSource, FacultyOutboxEventSource>();

        // RPT-3 (release/DEVELOPMENT_PLAN.md Flow #22, Reporting): Library's own outward-facing
        // reporting-query contract - see CrossModule.LibraryReportingQueryAdapter's own remarks.
        services.AddScoped<UMS.Shared.Library.ILibraryReportingQuery, CrossModule.LibraryReportingQueryAdapter>();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPermissionManifest, LibraryPermissionManifest>();
        services.AddSingleton(new LibraryOptions
        {
            MaxConcurrentLoansStudent = configuration.GetValue<int?>("Library:MaxConcurrentLoansStudent") ?? 3,
            MaxConcurrentLoansFaculty = configuration.GetValue<int?>("Library:MaxConcurrentLoansFaculty") ?? 10,
            LoanPeriodDaysStudent = configuration.GetValue<int?>("Library:LoanPeriodDaysStudent") ?? 14,
            LoanPeriodDaysFaculty = configuration.GetValue<int?>("Library:LoanPeriodDaysFaculty") ?? 30,
            MaxRenewalCount = configuration.GetValue<int?>("Library:MaxRenewalCount") ?? 2,
            ReservationClaimWindowHours = configuration.GetValue<int?>("Library:ReservationClaimWindowHours") ?? 48,
            FineDailyRateBdt = configuration.GetValue<decimal?>("Library:FineDailyRateBdt") ?? 10m,
            DefaultReplacementCostBdt = configuration.GetValue<decimal?>("Library:DefaultReplacementCostBdt") ?? 500m,
        });

        services.AddScoped<INotificationRequestPublisher, NotificationRequestIntakeAdapter>();
        services.AddScoped<BorrowerContextService>();

        services.AddScoped<CatalogService>();
        services.AddScoped<BookSearchService>();
        services.AddScoped<DigitalResourceAccessService>();
        services.AddScoped<LostCopyWriteOffService>();
        services.AddScoped<LoanService>();
        services.AddScoped<LoanOverdueDetectionService>();
        services.AddScoped<LoanReviewFlagService>();
        services.AddScoped<ReservationService>();
        services.AddScoped<ReservationFulfillmentService>();
        services.AddScoped<FineAccrualService>();
        services.AddScoped<FineService>();
        services.AddScoped<FinePaymentConfirmationService>();

        return services;
    }

    /// <summary>Applies pending EF Core migrations for the <c>library</c> schema - called once from the Host/Workers composition root, mirroring every other module's own <c>UseXModuleAsync</c>.</summary>
    public static async Task UseLibraryModuleAsync(this IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);
    }
}
