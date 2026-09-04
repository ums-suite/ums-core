using UMS.Modules.Notifications.Domain.Common;

namespace UMS.Modules.Notifications.Infrastructure.Providers;

/// <summary>NTF-10: the SMS channel adapter (<c>ISmsProvider</c> per ADR-0009's naming) - see <see cref="HttpChannelProviderBase"/> for the shared HTTP shape and <see cref="FakeProviderPrimaryHandler"/> for why this is a fake gateway, not real Twilio/a local BSP.</summary>
internal sealed class HttpSmsChannelProvider(HttpClient httpClient) : HttpChannelProviderBase(httpClient)
{
    public override NotificationChannel Channel => NotificationChannel.Sms;
}
