using UMS.Modules.Notifications.Domain.Common;

namespace UMS.Modules.Notifications.Infrastructure.Providers;

/// <summary>NTF's WhatsApp channel (requirement-spec.md §9 Open Questions: "a first-class delivery channel for urgent/OTP-class messages ... the exact provider (Meta's own Business API vs. a BSP reseller) is a build-time default, not fixed here"). See <see cref="HttpChannelProviderBase"/>/<see cref="FakeProviderPrimaryHandler"/> for why this is a fake gateway, not a real Meta WhatsApp Business API integration.</summary>
internal sealed class HttpWhatsAppChannelProvider(HttpClient httpClient) : HttpChannelProviderBase(httpClient)
{
    public override NotificationChannel Channel => NotificationChannel.WhatsApp;
}
