using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using UMS.Modules.Academic.Infrastructure;
using UMS.Modules.Admission.Infrastructure;
using UMS.Modules.Alumni.Infrastructure;
using UMS.Modules.Audit.Infrastructure;
using UMS.Modules.Career.Infrastructure;
using UMS.Modules.Content.Infrastructure;
using UMS.Modules.Documents.Infrastructure;
using UMS.Modules.Faculty.Infrastructure;
using UMS.Modules.Finance.Infrastructure;
using UMS.Modules.Hostel.Infrastructure;
using UMS.Modules.Identity.Infrastructure;
using UMS.Modules.Learning.Infrastructure;
using UMS.Modules.Library.Infrastructure;
using UMS.Modules.Notifications.Infrastructure;
using UMS.Modules.Organization.Infrastructure;
using UMS.Modules.Reporting.Infrastructure;
using UMS.Modules.Research.Infrastructure;
using UMS.Modules.Student.Infrastructure;
using UMS.Shared.Observability;
using UMS.Shared.Resilience;
using UMS.Workers;
using UMS.Workers.Admission;
using UMS.Workers.Alumni;
using UMS.Workers.AuditExports;
using UMS.Workers.BulkDocumentGeneration;
using UMS.Workers.Career;
using UMS.Workers.Content;
using UMS.Workers.Faculty;
using UMS.Workers.Finance;
using UMS.Workers.Hostel;
using UMS.Workers.Learning;
using UMS.Workers.Library;
using UMS.Workers.Notifications;
using UMS.Workers.Reporting;
using UMS.Workers.Research;
using UMS.Workers.Student;

var builder = WebApplication.CreateBuilder(args);

builder.AddUmsObservability(serviceName: "ums-workers");

builder.Services.AddHostedService<Worker>();

// Audit's export relay (AUD-9/AUD-10, ADR-0014) - the first real worker to run on this host.
// Registers the same module DI (repositories, IObjectStorage, IOutboxReader) the Host uses for
// Audit's own read/write path, since both processes share one physical Postgres database
// (ADR-0001).
builder.Services.AddAuditModule(builder.Configuration);
builder.Services.AddHostedService<AuditExportRelayWorker>();

// Documents' bulk/async generation path (DOC-4/DOC-6/DOC-7), the object-storage-outage retry
// relay (DOC-15), and the compensating stale-claim sweep (edge-cases.md's object-storage/DB-
// ordering decision) - the three background pieces Documents needs on this host, mirroring
// Audit's own export relay registration immediately above.
builder.Services.AddDocumentsModule(builder.Configuration);
builder.Services.AddHostedService<BulkGenerationRelayWorker>();
builder.Services.AddHostedService<DocumentGenerationRetryRelayWorker>();
builder.Services.AddHostedService<PendingDocumentSweepWorker>();

// UMS.Shared.Resilience's Redis multiplexer (ADR-0007) - Notifications' OTP rate limiter and every
// channel provider's Polly-wrapped HttpClient (NTF-9/10/11 + WhatsApp) both need it.
await builder.Services.AddUmsResilienceAsync(builder.Configuration);

// Organization registered before Identity - Identity.Infrastructure's own DI now resolves
// UMS.Shared.Organization.IOrganizationNodeExistenceChecker (Flow #6) unconditionally as part of
// AddIdentityModule, the same real dependency the Host composition root already satisfies.
builder.Services.AddOrganizationModule(builder.Configuration);

// Identity registered here only so Notifications' own UMS.Shared.Identity.IRecipientDirectory
// cross-module read path (NTF-2) has a real implementation to resolve in THIS process too - this
// worker process never serves an HTTP request of its own, so none of Identity's authentication/
// authorization wiring is exercised here.
builder.Services.AddIdentityModule(builder.Configuration);

// Notifications' per-channel dispatch workers (NTF-13's retry/dead-letter loop, NTF-16's
// bulk/backpressure-aware prioritized dispatch) - one independent BackgroundService per channel so
// a backed-up channel (edge-cases.md's "SMS provider outage") never delays another channel's own
// dispatch (ADR-0009). Also gives Documents' DOC-13 call into
// UMS.Shared.Notifications.INotificationRequestIntake a real implementation to resolve when the
// bulk generation path runs from this process.
builder.Services.AddNotificationsModule(builder.Configuration);
builder.Services.AddHostedService<EmailDispatchWorker>();
builder.Services.AddHostedService<SmsDispatchWorker>();
builder.Services.AddHostedService<WhatsAppDispatchWorker>();
builder.Services.AddHostedService<PushDispatchWorker>();
builder.Services.AddHostedService<InAppDispatchWorker>();

// Faculty (release/DEVELOPMENT_PLAN.md Flow #10) - the CourseAssignment projection relay (FAC-4)
// and the LeaveApproved/LeaveRejected notification relay (FAC-12), mirroring Documents' own two
// relay registrations immediately above. Depends on Organization (registered above) for
// UMS.Shared.Organization.IOrganizationNodeExistenceChecker and on Notifications (registered
// above) for UMS.Shared.Notifications.INotificationRequestIntake.
builder.Services.AddFacultyModule(builder.Configuration);
builder.Services.AddHostedService<CourseAssignmentProjectionRelayWorker>();
builder.Services.AddHostedService<LeaveNotificationRelayWorker>();

// Learning (release/DEVELOPMENT_PLAN.md Flow #13) - three relays: LRN-8's PlagiarismCheck dispatch
// (drains SubmissionCreated from Learning's own outbox, then drives every queued check through the
// Polly-wrapped provider), LRN-2/LRN-9's hardCloseAt window-close sweep (closes elapsed Assignments
// and re-enqueues any counted Submission still lacking a completed check), and the Notifications
// fan-out relay. Student and Academic are registered immediately above it - neither for a worker of
// its own, but because Learning's own DI resolves UMS.Shared.Academic.ICourseOfferingLookup, whose
// real implementation in turn resolves UMS.Shared.Faculty.IFacultyMemberLookup and
// UMS.Shared.Student.IStudentStatusChecker. Same reason Notifications' recipient lookup forces
// Identity to be registered in this process (see that registration's own remark): a worker process
// must satisfy every cross-module contract the modules it hosts actually resolve, exactly as the
// Host does.
builder.Services.AddStudentModule(builder.Configuration);
builder.Services.AddAcademicModule(builder.Configuration);
builder.Services.AddLearningModule(builder.Configuration);
builder.Services.AddHostedService<PlagiarismCheckDispatchWorker>();
builder.Services.AddHostedService<AssignmentWindowCloseWorker>();
builder.Services.AddHostedService<LearningNotificationRelayWorker>();

// release/DEVELOPMENT_PLAN.md Flow #16 (Student - Admission Integration, STU-15) - the bulk-import
// relay, mirroring Admission's/Documents' own bulk-job relay registrations above. Student is already
// registered immediately above (for Learning's own cross-module chain); this worker is the first one
// in this process to actually resolve Student's own repositories/services directly.
builder.Services.AddHostedService<StudentBulkImportRelayWorker>();

// Finance — Payment Core + remainder (release/DEVELOPMENT_PLAN.md Flows #14/#18) - three background
// pieces mirroring Documents'/Faculty's/Learning's own registrations immediately above: FIN-9/FIN-10's
// stuck-payment sweep (checks the fake gateway directly for any Payment stuck Initiated/Pending past
// its own timeout), FIN-16's Notifications fan-out relay (now also carrying FIN-11's RefundCompleted),
// and FIN-14's daily reconciliation job (compares Successful PaymentTransactions against the fake
// gateway's own settlement report, flagging any mismatch as a ReconciliationException for manual
// review). Depends on Identity only (module-boundaries.md), already registered above, plus Documents
// (FIN-15) and Notifications (FIN-16), both registered above too.
builder.Services.AddFinanceModule(builder.Configuration);
builder.Services.AddHostedService<StuckPaymentSweepWorker>();
builder.Services.AddHostedService<FinanceNotificationRelayWorker>();
builder.Services.AddHostedService<ReconciliationWorker>();

// Admission (release/DEVELOPMENT_PLAN.md Flow #15) - four relays mirroring the registrations
// immediately above: the Finance-payment-confirmation relay (design-decisions.md's own
// cross-module-outbox-polling mechanism, reading finance.outbox_messages directly - Finance itself
// is already registered above), the ExamAttempt timeout sweep (ADM-13), the PublishJob relay
// (ADR-0007's write-through cache + ADM-18's bulk fan-out), and the Notifications relay. Depends on
// Identity, Organization, Finance, Documents, and Notifications (module-boundaries.md), all already
// registered above.
builder.Services.AddAdmissionModule(builder.Configuration);
builder.Services.AddHostedService<ApplicationPaymentConfirmationRelayWorker>();
builder.Services.AddHostedService<ExamAttemptTimeoutSweepWorker>();
builder.Services.AddHostedService<PublishJobRelayWorker>();
builder.Services.AddHostedService<AdmissionNotificationRelayWorker>();

// Hostel (Flow #19) - five background pieces: HOS-9's Finance-payment relay (mirrors Admission's
// own ApplicationPaymentConfirmationRelayWorker, reading finance.outbox_messages directly - Finance
// already registered above), HOS-17's Student-status relay (the platform's first consumer of
// Student's own outbox, mirroring the same pattern against student."OutboxMessages" - Student
// already registered above for Learning's own chain), HOS-10's grace-period sweep, HOS-14's
// waitlist re-ranking relay (polls Hostel's OWN outbox, event-driven off AllocationCheckedOut/
// AllocationExpired), and the Notifications fan-out relay. Depends on Identity, Student, and
// Finance (module-boundaries.md), all already registered above.
builder.Services.AddHostelModule(builder.Configuration);
builder.Services.AddHostedService<HostelFinancePaymentRelayWorker>();
builder.Services.AddHostedService<HostelStudentStatusRelayWorker>();
builder.Services.AddHostedService<HostelGracePeriodSweepWorker>();
builder.Services.AddHostedService<HostelWaitlistReRankingRelayWorker>();
builder.Services.AddHostedService<HostelNotificationRelayWorker>();

// Library (Flow #20) - eight background pieces: LIB-13's Finance-payment relay (mirrors Hostel's/
// Admission's own, reading finance.outbox_messages directly), LIB-16's Student- and Faculty-status
// relays (Library is the platform's first consumer of BOTH outboxes for the same purpose, since a
// Loan's borrower can be either), LIB-9's reservation-fulfillment relay (polls Library's OWN outbox
// for LoanReturned, event-driven exactly like HostelWaitlistReRankingRelayWorker) and its
// claim-window expiry sweep, LIB-10's overdue-detection sweep, LIB-11's fine-accrual sweep, and the
// Notifications fan-out relay. Depends on Identity, Student, Faculty, and Finance
// (module-boundaries.md), all already registered above.
builder.Services.AddLibraryModule(builder.Configuration);
builder.Services.AddHostedService<LibraryFinancePaymentRelayWorker>();
builder.Services.AddHostedService<LibraryStudentStatusRelayWorker>();
builder.Services.AddHostedService<LibraryFacultyStatusRelayWorker>();
builder.Services.AddHostedService<LibraryReservationFulfillmentRelayWorker>();
builder.Services.AddHostedService<LibraryReservationExpirySweepWorker>();
builder.Services.AddHostedService<LibraryOverdueDetectionSweepWorker>();
builder.Services.AddHostedService<LibraryFineAccrualSweepWorker>();
builder.Services.AddHostedService<LibraryNotificationRelayWorker>();

// Content (Flow #24) - CNT-4/CNT-9/CNT-11's shared idempotent, lock-free scan-and-transition sweep
// (design-decisions.md "Scheduled-Publish Job Exactly-Once Execution Mechanism" - no Redis
// lease/lock, unlike Reporting's MetricRefreshJobBase below), plus CNT-13's urgent-notice
// Notifications fan-out relay. Depends on Identity, Organization, Notifications, and Documents
// (module-boundaries.md), all already registered above.
builder.Services.AddContentModule(builder.Configuration);
builder.Services.AddHostedService<NoticeSchedulingSweepWorker>();
builder.Services.AddHostedService<BannerSchedulingSweepWorker>();
builder.Services.AddHostedService<DownloadResourceSchedulingSweepWorker>();
builder.Services.AddHostedService<NoticeNotificationRelayWorker>();

// Research (Flow #25) - three background pieces: RES-12's daily lapsed-embargo sweep (design-
// decisions.md's own worker-over-lazy-evaluation decision, keeping the public showcase genuinely
// HTTP-cacheable), RES-5's Faculty-status relay (the platform's newest consumer of Faculty's own
// outbox, mirroring Library's own FacultyStatusRelayWorker exactly against the same
// faculty."OutboxMessages" table), and the Notifications fan-out relay (GrantFunded/GrantClosed/
// GrantReported/GrantPiReassignmentRequired/InstitutionalRepositoryEntryEmbargoLifted). Depends on
// Identity, Organization, and Faculty (module-boundaries.md) - all already registered above.
builder.Services.AddResearchModule(builder.Configuration);
builder.Services.AddHostedService<ResearchEmbargoLiftSweepWorker>();
builder.Services.AddHostedService<ResearchFacultyStatusRelayWorker>();
builder.Services.AddHostedService<ResearchNotificationRelayWorker>();

// Alumni (Flow #29) - five background pieces: ALM-1's StudentGraduated relay (polls Student's own
// outbox, mirroring ResearchFacultyStatusRelayWorker's own shape), ALM-9's Finance-payment relay
// (polls finance.outbox_messages, mirroring ApplicationPaymentConfirmationRelayWorker exactly),
// ALM-6's JobPosting expiry sweep (mirrors Content's own NoticeSchedulingSweepWorker), ALM-10's
// recurring-donation scheduler, and the ALM-15 Notifications fan-out relay. Depends on Identity,
// Student (via UMS.Shared.Student.IStudentStatusChecker), and Finance - all already registered above.
builder.Services.AddAlumniModule(builder.Configuration);
builder.Services.AddHostedService<AlumniStudentGraduatedRelayWorker>();
builder.Services.AddHostedService<AlumniFinancePaymentRelayWorker>();
builder.Services.AddHostedService<JobPostingExpirySweepWorker>();
builder.Services.AddHostedService<RecurringDonationSchedulerWorker>();
builder.Services.AddHostedService<AlumniNotificationRelayWorker>();

// Career (release/DEVELOPMENT_PLAN.md Flow #30) - three background pieces: CAR-16's Student status
// relay (polls Student's own outbox for StudentGraduated/StudentStatusChanged, mirroring
// AlumniStudentGraduatedRelayWorker's own shape - deliberately performs NO mutation to any existing
// CareerApplication), CAR-3's Internship deadline sweep (mirrors JobPostingExpirySweepWorker), and
// the CAR-17 Notifications fan-out relay (InternshipPublished/CareerApplicationStatusChanged/
// InterviewSlotBooked - CareerApplicationCancelled is deliberately excluded, already published
// synchronously by the withdrawal/cancellation cascade handlers themselves). Depends on Identity,
// Student, Organization, and Documents - all already registered above; ZERO dependency on Alumni,
// Academic, or Finance.
builder.Services.AddCareerModule(builder.Configuration);
builder.Services.AddHostedService<CareerStudentStatusRelayWorker>();
builder.Services.AddHostedService<InternshipDeadlineSweepWorker>();
builder.Services.AddHostedService<CareerNotificationRelayWorker>();

// Reporting (release/DEVELOPMENT_PLAN.md Flow #22, topped up by Flow #26 "Reporting - Content &
// Research top-up") - ADR-0013's "depends on everything" module: eight independent
// per-dashboard-family refresh workers (RPT-1/RPT-4..9 plus Flow #26's Research/Content additions,
// design-decisions.md - no single mega-scheduler), each acquiring RPT-1's Redis lease before
// running, plus the RegulatoryReportRun poll relay (RPT-12/RPT-13). Depends on Academic, Admission,
// Finance, Faculty, Hostel, Library, Research, and Content's own read-only reporting-query
// contracts, Documents (RPT-13 PDF generation), and Notifications, all already registered above.
// ResearchMetricRefreshWorker has no Admin-facing dashboard route of its own (see
// ResearchDashboardRefreshService's own remarks) - it exists solely to keep the "research-dashboard"
// DashboardMetric fresh for the regulatory-report pipeline's "Research" category.
builder.Services.AddReportingModule(builder.Configuration);
builder.Services.AddHostedService<AcademicMetricRefreshWorker>();
builder.Services.AddHostedService<AdmissionMetricRefreshWorker>();
builder.Services.AddHostedService<FinancialMetricRefreshWorker>();
builder.Services.AddHostedService<FacultyMetricRefreshWorker>();
builder.Services.AddHostedService<HostelMetricRefreshWorker>();
builder.Services.AddHostedService<LibraryMetricRefreshWorker>();
builder.Services.AddHostedService<ResearchMetricRefreshWorker>();
builder.Services.AddHostedService<ContentMetricRefreshWorker>();
builder.Services.AddHostedService<RegulatoryReportRunRelayWorker>();

// Same readiness contract as UMS.Host (ums-conventions.md, Observability: "UMS.Workers exposes
// the same two endpoints"). Per-job outbox/queue-depth checks (ADR-0014) are added once the first
// real worker (module-owned outbox relay) exists.
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

// Idempotent (CREATE TABLE IF NOT EXISTS partitions; EF's own migrations-history check) - safe to
// also run from this second process regardless of whichever of UMS.Host/UMS.Workers happens to
// start first (ADR-0001's single physical database).
await app.Services.UseAuditModuleAsync();
await app.Services.UseDocumentsModuleAsync();
await app.Services.UseOrganizationModuleAsync();
await app.Services.UseIdentityModuleAsync();
await app.Services.UseNotificationsModuleAsync();
await app.Services.UseFacultyModuleAsync();
await app.Services.UseStudentModuleAsync();
await app.Services.UseAcademicModuleAsync();
await app.Services.UseLearningModuleAsync();
await app.Services.UseFinanceModuleAsync();
await app.Services.UseAdmissionModuleAsync();
await app.Services.UseHostelModuleAsync();
await app.Services.UseLibraryModuleAsync();
await app.Services.UseContentModuleAsync();
await app.Services.UseResearchModuleAsync();
await app.Services.UseReportingModuleAsync();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.Run();
