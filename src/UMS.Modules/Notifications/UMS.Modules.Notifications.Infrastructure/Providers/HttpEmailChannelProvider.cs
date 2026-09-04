using UMS.Modules.Notifications.Domain.Common;

namespace UMS.Modules.Notifications.Infrastructure.Providers;

/// <summary>NTF-9: the email channel adapter (<c>IEmailProvider</c> per ADR-0009's naming) - see <see cref="HttpChannelProviderBase"/> for the shared HTTP shape and <see cref="FakeProviderPrimaryHandler"/> for why this is a fake gateway, not real SendGrid/SES.</summary>
internal sealed class HttpEmailChannelProvider(HttpClient httpClient) : HttpChannelProviderBase(httpClient)
{
    public override NotificationChannel Channel => NotificationChannel.Email;
}
