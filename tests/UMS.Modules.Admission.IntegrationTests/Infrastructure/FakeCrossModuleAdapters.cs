using UMS.Shared.Documents;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Finance;
using UMS.Shared.Identity;
using UMS.Shared.Integrations;
using UMS.Shared.Notifications;
using UMS.Shared.Organization;
using UMS.Shared.Student;

namespace UMS.Modules.Admission.IntegrationTests.Infrastructure;

/// <summary>
/// This suite tests Admission's OWN concurrency/state-machine invariants against a real Postgres +
/// Redis (Testcontainers) - Organization/Finance/Documents/Identity/Notifications/Student are
/// genuinely different modules with their own already-verified test suites, so faking their
/// cross-module contracts here keeps this suite's own fixture focused on what it actually exercises,
/// mirroring Finance's own <c>FakeCrossModuleAdapters</c> posture exactly.
/// </summary>
internal sealed class FakeOrganizationNodeExistenceChecker : IOrganizationNodeExistenceChecker
{
    public Task<bool> ExistsAsync(Guid organizationNodeId, CancellationToken cancellationToken = default) => Task.FromResult(true);
}

internal sealed class FakeInvoiceRequester : IInvoiceRequester
{
    public Task<Result<InvoiceSummary>> CreateInvoiceAsync(CreateInvoiceCommand command, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success(new InvoiceSummary(Guid.NewGuid(), "Open", 500m, "BDT")));
}

internal sealed class FakeDocumentGenerationRequester : IDocumentGenerationRequester
{
    public Task<Result<GeneratedDocumentSummary>> RequestAsync(RequestDocumentGenerationCommand command, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success(new GeneratedDocumentSummary(Guid.NewGuid(), "Ready", "https://fake-documents.internal/doc.pdf")));
}

internal sealed class FakeUserProvisioner : IUserProvisioner
{
    public Task<Result<ProvisionedUserSummary>> ProvisionAsync(ProvisionUserCommand command, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success(new ProvisionedUserSummary(Guid.NewGuid(), command.Username, command.Email)));
}

internal sealed class FakeNotificationRequestIntake : INotificationRequestIntake
{
    public Task<Result<Guid>> SubmitAsync(SubmitNotificationRequestCommand request, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success(Guid.NewGuid()));
}

internal sealed class FakeStudentRecordProvisioner : IStudentRecordProvisioner
{
    public Task<Result<StudentRecordSummary>> CreateAsync(CreateStudentRecordCommand command, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success(new StudentRecordSummary(Guid.NewGuid(), "2026-CSE-000001", Guid.NewGuid(), "Enrolled")));
}

internal sealed class FakeProctoringProvider : IProctoringProvider
{
    public Task<Result<ProctoringSessionHandle>> StartSessionAsync(StartProctoringSessionCommand command, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success(new ProctoringSessionHandle($"fake-{command.AttemptId:N}", IdentityVerified: true, LockdownEnforced: true)));

    public Task<Result<ProctoringAnomalyReport>> ReportAnomalyAsync(ReportProctoringAnomalyCommand command, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success(new ProctoringAnomalyReport(command.AnomalyType, 1.0m, command.OccurredAt)));
}
