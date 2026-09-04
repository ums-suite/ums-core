using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Faculty.Application.Abstractions;
using UMS.Modules.Faculty.Application.CourseAssignments;

namespace UMS.Workers.Faculty;

/// <summary>
/// FAC-4: polls Academic's outbox (<see cref="IInstructorAssignmentEventSource"/>) for
/// `InstructorAssigned`/`InstructorUnassigned` and applies each to Faculty's own `CourseAssignment`
/// projection (design-decisions.md, "CourseAssignment Projection-Update Mechanism and Latency
/// Guarantee") - mirrors the shape of Documents' <c>DocumentGenerationRetryRelayWorker</c> exactly,
/// polling on a short fixed interval rather than anything push-based, per that same design
/// decision.
/// </summary>
public sealed class CourseAssignmentProjectionRelayWorker(IServiceScopeFactory scopeFactory, ILogger<CourseAssignmentProjectionRelayWorker> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "CourseAssignment projection relay: unexpected failure while processing pending InstructorAssigned/InstructorUnassigned events.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var eventSource = scope.ServiceProvider.GetRequiredService<IInstructorAssignmentEventSource>();
        var projection = scope.ServiceProvider.GetRequiredService<CourseAssignmentProjectionService>();

        var envelopes = await eventSource.GetUnprocessedAsync(BatchSize, cancellationToken).ConfigureAwait(false);
        foreach (var envelope in envelopes)
        {
            await ProcessOneAsync(eventSource, projection, envelope, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessOneAsync(IInstructorAssignmentEventSource eventSource, CourseAssignmentProjectionService projection, InstructorAssignmentEventEnvelope envelope, CancellationToken cancellationToken)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<InstructorAssignmentPayload>(envelope.PayloadJson)
                ?? throw new InvalidOperationException($"Empty {envelope.EventType} payload.");

            var result = envelope.EventType switch
            {
                "InstructorAssigned" => await projection.ApplyInstructorAssignedAsync(payload, envelope.OccurredAt, envelope.EventId.ToString(), cancellationToken).ConfigureAwait(false),
                "InstructorUnassigned" => await projection.ApplyInstructorUnassignedAsync(payload, envelope.OccurredAt, envelope.EventId.ToString(), cancellationToken).ConfigureAwait(false),
                _ => throw new InvalidOperationException($"Unknown event type '{envelope.EventType}'."),
            };

            if (result.IsFailure)
            {
                logger.LogWarning("CourseAssignment projection relay: applying event {EventId} ({EventType}) failed - {Error}. Will retry next poll.", envelope.EventId, envelope.EventType, result.Error!.Message);
                return;
            }

            await eventSource.MarkProcessedAsync(envelope.EventId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "CourseAssignment projection relay: unhandled failure applying event {EventId} ({EventType}).", envelope.EventId, envelope.EventType);
        }
    }
}
