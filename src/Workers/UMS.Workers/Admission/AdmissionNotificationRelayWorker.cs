using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Admission.Application.Abstractions;
using UMS.Modules.Admission.Domain.Events;

namespace UMS.Workers.Admission;

/// <summary>Fans Admission's own outbox events out through <see cref="INotificationRequestPublisher"/> (ADR-0009) - mirroring Finance's/Learning's own NotificationRelayWorker exactly.</summary>
public sealed class AdmissionNotificationRelayWorker(IServiceScopeFactory scopeFactory, ILogger<AdmissionNotificationRelayWorker> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private static readonly string[] HandledEventTypes =
    [
        nameof(ApplicantRegistered),
        nameof(ApplicationSubmitted),
        nameof(AdmissionConfirmed),
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach (var eventType in HandledEventTypes)
                {
                    await ProcessPendingAsync(eventType, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Admission notification relay: unexpected failure while processing pending events.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private static AdmissionNotificationRequest ToRequest(string eventType, string payloadJson, Guid outboxMessageId) => eventType switch
    {
        nameof(ApplicantRegistered) => FromApplicantRegistered(Deserialize<ApplicantRegistered>(payloadJson), outboxMessageId),
        nameof(ApplicationSubmitted) => FromApplicationSubmitted(Deserialize<ApplicationSubmitted>(payloadJson), outboxMessageId),
        nameof(AdmissionConfirmed) => FromAdmissionConfirmed(Deserialize<AdmissionConfirmed>(payloadJson), outboxMessageId),
        _ => throw new InvalidOperationException($"Unknown Admission notification event type '{eventType}'."),
    };

    private static T Deserialize<T>(string payloadJson) =>
        JsonSerializer.Deserialize<T>(payloadJson) ?? throw new InvalidOperationException($"Empty {typeof(T).Name} payload.");

    private static AdmissionNotificationRequest FromApplicantRegistered(ApplicantRegistered evt, Guid outboxMessageId) =>
        new(evt.IdentityUserId, nameof(ApplicantRegistered), evt.ApplicantId.ToString(), new Dictionary<string, string>(), outboxMessageId.ToString());

    private static AdmissionNotificationRequest FromApplicationSubmitted(ApplicationSubmitted evt, Guid outboxMessageId) =>
        new(evt.ApplicantId, nameof(ApplicationSubmitted), evt.ApplicationId.ToString(), new Dictionary<string, string> { ["applicationNumber"] = evt.ApplicationNumber }, outboxMessageId.ToString());

    private static AdmissionNotificationRequest FromAdmissionConfirmed(AdmissionConfirmed evt, Guid outboxMessageId) =>
        new(evt.ApplicantId, nameof(AdmissionConfirmed), evt.ApplicationId.ToString(), new Dictionary<string, string>(), outboxMessageId.ToString());

    private async Task ProcessPendingAsync(string eventType, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxReader>();
        var publisher = scope.ServiceProvider.GetRequiredService<INotificationRequestPublisher>();

        var messages = await outbox.GetUnprocessedAsync(eventType, BatchSize, cancellationToken).ConfigureAwait(false);
        foreach (var message in messages)
        {
            try
            {
                var request = ToRequest(eventType, message.PayloadJson, message.Id);
                await publisher.PublishAsync(request, cancellationToken).ConfigureAwait(false);
                await outbox.MarkProcessedAsync(message.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await outbox.RecordFailedAttemptAsync(message.Id, ex.Message, cancellationToken).ConfigureAwait(false);
                logger.LogWarning(ex, "Admission notification relay: publish failed for outbox message {MessageId} - will retry next poll.", message.Id);
            }
        }
    }
}
