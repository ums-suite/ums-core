using UMS.Modules.Notifications.Domain.Common;

namespace UMS.Modules.Notifications.Infrastructure.Providers;

/// <summary>
/// NTF-11: the push channel adapter. §9 Open Questions leaves the concrete provider (FCM/APNs)
/// unnamed - out of this spec's authority - so this build's fake gateway stands in for whichever is
/// chosen later; the adapter seam itself (this class + <see cref="HttpChannelProviderBase"/>) is
/// real and provider-agnostic. In this environment every Push send resolves <c>NoContactInfo</c>
/// before ever reaching this class, since Identity has no device/push-token registry yet (see
/// <c>UMS.Shared.Identity.RecipientContactInfo</c>'s own remarks) - this class's own send logic is
/// still directly unit-tested against the fake gateway to prove the HTTP/Polly wiring itself works.
/// </summary>
internal sealed class HttpPushChannelProvider(HttpClient httpClient) : HttpChannelProviderBase(httpClient)
{
    public override NotificationChannel Channel => NotificationChannel.Push;
}
