using UMS.Modules.Notifications.Application.Webhooks;

namespace UMS.Modules.Notifications.Api.Contracts;

/// <summary>Shaped like a real email/SMS gateway's own bounce/delivery-receipt callback (NTF-15) - see <see cref="ProviderWebhookService"/>'s own remarks on how this build's fake gateway relates to this endpoint.</summary>
public sealed record ProviderWebhookRequestBody(string ProviderMessageId, ProviderWebhookEvent Event, string? Reason);
