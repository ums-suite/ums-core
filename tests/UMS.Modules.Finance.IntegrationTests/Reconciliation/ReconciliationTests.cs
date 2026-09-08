using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Finance.Application.Payments;
using UMS.Modules.Finance.Application.Reconciliation;
using UMS.Modules.Finance.Infrastructure.Gateway;
using UMS.Modules.Finance.Infrastructure.Persistence;
using UMS.Modules.Finance.IntegrationTests.Infrastructure;

namespace UMS.Modules.Finance.IntegrationTests.Reconciliation;

/// <summary>
/// FIN-14/ADR-0014: requirement-spec.md §8's mismatch edge case ("never a silent balance adjustment")
/// and edge-cases.md "Daily Reconciliation Job Racing a Live In-Flight Payment" - design-decisions.md's
/// buffer-window + row-lock combination, against a real Postgres.
/// </summary>
[Collection(FinanceApiTestCollectionDefinition.Name)]
public sealed class ReconciliationTests(FinanceServiceFixture fixture)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task A_Payment_the_gateway_settlement_report_confirms_Successful_is_marked_Reconciled()
    {
        var feeType = $"ApplicationFee-{Guid.NewGuid():N}";
        var ownerId = Guid.NewGuid();
        var payment = await FinanceTestData.SeedSuccessfulPaymentAsync(fixture.Services, feeType, ownerId, amount: 500m);

        // The fake gateway's own /init call already recorded this transaction as "Pending" - flip it
        // to "Successful" to simulate the gateway's settlement report confirming the same outcome
        // Finance's own webhook already recorded (FakeSslCommerzGatewayState.SetStatus preserves the
        // already-aligned gatewayTransactionId - see FinanceTestData's own remarks).
        fixture.GatewayState.SetStatus(payment.Id.ToString(), "Successful");
        await BackdateUpdatedAtAsync(payment.Id, TimeSpan.FromMinutes(31));

        using var scope = fixture.Services.CreateScope();
        // Asserted per-Payment, not against the run's own aggregate Candidates/Reconciled/Flagged
        // totals: this suite shares one Postgres fixture across many tests (FinanceApiTestCollectionDefinition),
        // and a mismatch test elsewhere in the class can legitimately leave its OWN backdated,
        // still-Successful Payment sitting in the candidate pool for a later run - that is a shared-
        // fixture artifact, not a correctness bug in THIS test's own Payment.
        await scope.ServiceProvider.GetRequiredService<ReconciliationService>().RunAsync(Today, batchSize: 50);

        var reloaded = await scope.ServiceProvider.GetRequiredService<PaymentService>().GetByIdAsync(payment.Id, ownerId);
        Assert.Equal("Reconciled", reloaded.Value.Status);
    }

    /// <summary>edge-cases.md's mismatch edge case: the gateway has no settlement record at all for this transaction on the target date.</summary>
    [Fact]
    public async Task A_Payment_with_no_gateway_settlement_record_at_all_is_flagged_never_reconciled()
    {
        var feeType = $"ApplicationFee-{Guid.NewGuid():N}";
        var ownerId = Guid.NewGuid();
        var payment = await FinanceTestData.SeedSuccessfulPaymentAsync(fixture.Services, feeType, ownerId, amount: 500m);

        fixture.GatewayState.Forget(payment.Id.ToString());
        await BackdateUpdatedAtAsync(payment.Id, TimeSpan.FromMinutes(31));

        using var scope = fixture.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ReconciliationService>().RunAsync(Today, batchSize: 50);

        // requirement-spec.md §8: never a silent balance adjustment - the Payment stays Successful,
        // NOT auto-corrected to Reconciled or anything else, pending manual Accountant review.
        var reloaded = await scope.ServiceProvider.GetRequiredService<PaymentService>().GetByIdAsync(payment.Id, ownerId);
        Assert.Equal("Successful", reloaded.Value.Status);

        var exceptionCount = await CountReconciliationExceptionsAsync(payment.Id, "NoSettlementRecordFound");
        Assert.Equal(1, exceptionCount);
    }

    /// <summary>edge-cases.md's mismatch edge case: the gateway's settlement report disagrees with Finance's own recorded status.</summary>
    [Fact]
    public async Task A_Payment_the_gateway_settlement_report_disagrees_with_is_flagged_never_reconciled()
    {
        var feeType = $"ApplicationFee-{Guid.NewGuid():N}";
        var ownerId = Guid.NewGuid();
        var payment = await FinanceTestData.SeedSuccessfulPaymentAsync(fixture.Services, feeType, ownerId, amount: 500m);

        // The gateway's own settlement report disagrees - it reports Failed for a transaction Finance
        // itself recorded as Successful via a signature-verified webhook.
        fixture.GatewayState.SetStatus(payment.Id.ToString(), "Failed");
        await BackdateUpdatedAtAsync(payment.Id, TimeSpan.FromMinutes(31));

        using var scope = fixture.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ReconciliationService>().RunAsync(Today, batchSize: 50);

        var reloaded = await scope.ServiceProvider.GetRequiredService<PaymentService>().GetByIdAsync(payment.Id, ownerId);
        Assert.Equal("Successful", reloaded.Value.Status);

        var exceptionCount = await CountReconciliationExceptionsAsync(payment.Id, "GatewayStatusDisagreement");
        Assert.Equal(1, exceptionCount);
    }

    /// <summary>design-decisions.md "Reconciliation Job Concurrency-Safety": a Payment that settled within the last 30 minutes is deferred to the NEXT run, never evaluated by this one.</summary>
    [Fact]
    public async Task A_Payment_that_settled_within_the_buffer_window_is_deferred_to_the_next_run()
    {
        var feeType = $"ApplicationFee-{Guid.NewGuid():N}";
        var ownerId = Guid.NewGuid();
        var payment = await FinanceTestData.SeedSuccessfulPaymentAsync(fixture.Services, feeType, ownerId, amount: 500m);
        fixture.GatewayState.SetStatus(payment.Id.ToString(), "Successful");

        // No backdating - this Payment settled moments ago, still well within the 30-minute buffer.
        // The gateway settlement report DOES already match (SetStatus above) - so if the buffer
        // window failed to exclude it, this run would incorrectly mark it Reconciled; staying
        // Successful is this test's own, contamination-immune proof of exclusion (see the "match"
        // test's own remarks on why this suite asserts per-Payment outcomes, not the run's aggregate
        // Candidates/Reconciled/Flagged totals, against a fixture shared across many tests).
        using var scope = fixture.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ReconciliationService>().RunAsync(Today, batchSize: 50);

        var reloaded = await scope.ServiceProvider.GetRequiredService<PaymentService>().GetByIdAsync(payment.Id, ownerId);
        Assert.Equal("Successful", reloaded.Value.Status);
    }

    [Fact]
    public async Task RunAsync_completes_without_throwing_when_it_finds_nothing_new_to_evaluate()
    {
        using var scope = fixture.Services.CreateScope();
        var exception = await Record.ExceptionAsync(() => scope.ServiceProvider.GetRequiredService<ReconciliationService>().RunAsync(Today, batchSize: 50));

        Assert.Null(exception);
    }

    /// <summary>
    /// edge-cases.md "Daily Reconciliation Job Racing a Live In-Flight Payment": the row lock
    /// design-decisions.md specifies protects the reconciliation job's own read-decide-write against
    /// a genuinely concurrent writer on the SAME Payment row - here, a duplicate/redelivered webhook
    /// for the identical PaymentTransaction, racing the job via real Task.WhenAll concurrency rather
    /// than a sequential call. Whichever commits first wins; the other is a safe no-op either way
    /// (PaymentTransaction's own monotonic ordering guard - see PaymentWebhookTests' identical
    /// scenario), so the ONLY thing this test asserts is that the race never corrupts or deadlocks:
    /// the Payment ends up in a single well-defined terminal state, not torn between the two writers.
    /// </summary>
    [Fact]
    public async Task The_reconciliation_job_racing_a_duplicate_live_webhook_on_the_same_row_never_corrupts_state()
    {
        var feeType = $"ApplicationFee-{Guid.NewGuid():N}";
        var ownerId = Guid.NewGuid();
        var payment = await FinanceTestData.SeedSuccessfulPaymentAsync(fixture.Services, feeType, ownerId, amount: 500m);
        fixture.GatewayState.SetStatus(payment.Id.ToString(), "Successful");
        await BackdateUpdatedAtAsync(payment.Id, TimeSpan.FromMinutes(31));

        var gatewayTransactionId = fixture.GatewayState.Query(payment.Id.ToString())!.Value.GatewayTransactionId;
        var duplicateWebhookBody = JsonSerializer.Serialize(new { merchantTransactionId = payment.Id.ToString(), gatewayTransactionId, status = "Successful" });
        var signature = WebhookSignatureCalculator.Compute("integration-test-webhook-secret", duplicateWebhookBody);

        async Task RunReconciliationAsync()
        {
            using var scope = fixture.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ReconciliationService>().RunAsync(Today, batchSize: 50);
        }

        async Task RedeliverWebhookAsync()
        {
            using var scope = fixture.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<PaymentWebhookService>().HandleAsync(payment.Id, duplicateWebhookBody, signature, "corr-race-webhook");
        }

        await Task.WhenAll(RunReconciliationAsync(), RedeliverWebhookAsync());

        using var verifyScope = fixture.Services.CreateScope();
        var reloaded = await verifyScope.ServiceProvider.GetRequiredService<PaymentService>().GetByIdAsync(payment.Id, ownerId);
        Assert.True(reloaded.IsSuccess);
        // The redelivered webhook can never move this Payment backward (it is already Successful at
        // worst) - reconciliation may or may not have won the race, but the end state must always be
        // one of these two well-defined outcomes, never something else.
        Assert.True(reloaded.Value.Status is "Successful" or "Reconciled");
    }

    private async Task BackdateUpdatedAtAsync(Guid paymentId, TimeSpan age)
    {
        var backdatedTo = DateTimeOffset.UtcNow - age;
        using var scope = fixture.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var affected = await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE finance.payments SET updated_at = {backdatedTo} WHERE id = {paymentId}");
        Assert.Equal(1, affected);
    }

    private async Task<int> CountReconciliationExceptionsAsync(Guid paymentId, string reason)
    {
        using var scope = fixture.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        return await dbContext.Database
            .SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM finance.reconciliation_exceptions WHERE payment_id = {paymentId} AND reason = {reason}")
            .SingleAsync();
    }
}
