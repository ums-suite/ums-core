namespace UMS.Modules.Notifications.Application.Webhooks;

public enum ProviderWebhookEvent
{
    Delivered = 0,
    SoftBounce = 1,
    HardBounce = 2,
}

public sealed record ProviderWebhookPayload(string ProviderMessageId, ProviderWebhookEvent Event, string? Reason);
