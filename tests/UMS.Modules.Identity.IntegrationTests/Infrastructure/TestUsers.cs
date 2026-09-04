using System.Net.Http.Json;
using UMS.Modules.Identity.Application.Auth;
using UMS.Modules.Identity.Application.Users;

namespace UMS.Modules.Identity.IntegrationTests.Infrastructure;

public static class TestUsers
{
    public const string DefaultPassword = "a-strong-p@ssw0rd";

    public static async Task<UserDto> ProvisionAsync(HttpClient client, string? password = null)
    {
        var username = $"user-{Guid.NewGuid():N}";
        var request = new ProvisionUserRequest(
            username,
            $"{username}@example.edu.bd",
            "Test",
            "User",
            null,
            null,
            null,
            null,
            password ?? DefaultPassword);

        var response = await client.PostAsJsonAsync("/api/v1/identity/users", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserDto>())!;
    }

    public static async Task<TokenPairResult> LoginAsync(HttpClient client, string identifier, string password = DefaultPassword)
    {
        var response = await client.PostAsJsonAsync("/api/v1/identity/auth/login", new { identifier, password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TokenPairResult>())!;
    }

    public static HttpRequestMessage WithBearerToken(this HttpRequestMessage request, string accessToken)
    {
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }
}
