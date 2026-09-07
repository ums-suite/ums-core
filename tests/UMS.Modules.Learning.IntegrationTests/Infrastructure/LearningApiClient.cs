using System.Net;
using System.Net.Http.Json;

namespace UMS.Modules.Learning.IntegrationTests.Infrastructure;

/// <summary>Thin request helpers so each test reads as the scenario it exercises rather than as HTTP plumbing - the same shape Academic's own suite builds inline per test class.</summary>
internal static class LearningApiClient
{
    public static Task<HttpResponseMessage> PostAsync(this HttpClient client, string path, string token, object? body = null) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = body is null ? null : JsonContent.Create(body),
        }.WithBearerToken(token));

    public static Task<HttpResponseMessage> GetAsync(this HttpClient client, string path, string token) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Get, path).WithBearerToken(token));

    public static async Task<T> PostAndReadAsync<T>(this HttpClient client, string path, string token, object? body = null)
    {
        var response = await client.PostAsync(path, token, body);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"POST {path} failed with {response.StatusCode}: {errorBody}");
        }

        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    public static async Task<T> GetAndReadAsync<T>(this HttpClient client, string path, string token)
    {
        var response = await client.GetAsync(path, token);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"GET {path} failed with {response.StatusCode}: {errorBody}");
        }

        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    /// <summary>Asserts a failed response carries the exact machine-readable <c>code</c> the domain returned - not just the right status class.</summary>
    public static async Task AssertProblemCodeAsync(this HttpResponseMessage response, HttpStatusCode expectedStatus, string expectedCode)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(expectedStatus == response.StatusCode, $"Expected {expectedStatus} but got {response.StatusCode}: {body}");
        Assert.Contains(expectedCode, body, StringComparison.Ordinal);
    }
}
