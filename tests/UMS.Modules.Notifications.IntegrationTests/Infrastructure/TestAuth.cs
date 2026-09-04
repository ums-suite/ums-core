using System.Net.Http.Headers;
using System.Net.Http.Json;
using UMS.Modules.Identity.Application.Auth;

namespace UMS.Modules.Notifications.IntegrationTests.Infrastructure;

public static class TestAuth
{
    public static async Task<string> LoginAsync(HttpClient client, string identifier, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/identity/auth/login", new { identifier, password });
        response.EnsureSuccessStatusCode();
        var tokens = await response.Content.ReadFromJsonAsync<TokenPairResult>();
        return tokens!.AccessToken;
    }

    public static HttpRequestMessage WithBearerToken(this HttpRequestMessage request, string accessToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }
}
