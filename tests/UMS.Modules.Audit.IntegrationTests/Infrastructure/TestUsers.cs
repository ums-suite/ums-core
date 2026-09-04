using System.Net.Http.Json;
using UMS.Modules.Identity.Application.Users;

namespace UMS.Modules.Audit.IntegrationTests.Infrastructure;

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
}
