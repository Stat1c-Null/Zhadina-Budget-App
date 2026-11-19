using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using Zhadina.Api.Features.Auth;

namespace Zhadina.Api.Tests;

public class SettingsEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string BasePath = "/api/settings";

    // Declared here rather than reusing the API's type so these tests pin the JSON contract the frontend relies on.
    private record SettingsDto(decimal? PriorEarnings);

    private async Task<HttpClient> SignedInClient()
    {
        var client = factory.CreateClient();
        var signUp = await client.PostAsJsonAsync(
            "/api/auth/sign-up",
            new CredentialsRequest($"user-{Guid.NewGuid():N}@example.com", "correct-horse-1"));
        signUp.EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<SettingsDto> Get(HttpClient client) =>
        (await client.GetFromJsonAsync<SettingsDto>(BasePath))!;

    private static async Task<SettingsDto> Put(HttpClient client, decimal? priorEarnings)
    {
        var response = await client.PutAsJsonAsync(BasePath, new { priorEarnings });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SettingsDto>())!;
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    public async Task Endpoints_RequireSignIn(string method)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), BasePath);
        if (method is "PUT")
            request.Content = JsonContent.Create(new { priorEarnings = 1m });

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_ReturnsUnsetSettingsForANewUser()
    {
        Assert.Equal(new SettingsDto(null), await Get(await SignedInClient()));
    }

    [Fact]
    public async Task Put_SavesTheSettings_AndGetReturnsThem()
    {
        var client = await SignedInClient();

        var saved = await Put(client, 52_000.75m);

        Assert.Equal(new SettingsDto(52_000.75m), saved);
        Assert.Equal(saved, await Get(client));
    }

    [Fact]
    public async Task Put_AgainReplacesTheEarlierValue()
    {
        var client = await SignedInClient();
        await Put(client, 100m);

        await Put(client, 250.5m);

        Assert.Equal(new SettingsDto(250.5m), await Get(client));
    }

    [Fact]
    public async Task Put_NullClearsTheValue()
    {
        var client = await SignedInClient();
        await Put(client, 100m);

        await Put(client, null);

        Assert.Equal(new SettingsDto(null), await Get(client));
    }

    [Fact]
    public async Task Put_AcceptsZeroAndTheLargestValue()
    {
        var client = await SignedInClient();

        Assert.Equal(0m, (await Put(client, 0m)).PriorEarnings);
        Assert.Equal(999_999_999_999.99m, (await Put(client, 999_999_999_999.99m)).PriorEarnings);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(12.345)]
    [InlineData(1000000000000.0)]
    public async Task Put_InvalidValue_ReturnsAFieldError_AndKeepsTheOldValue(double priorEarnings)
    {
        var client = await SignedInClient();
        await Put(client, 100m);

        var response = await client.PutAsJsonAsync(BasePath, new { priorEarnings });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(["priorEarnings"], problem!.Errors.Keys);
        Assert.Equal(new SettingsDto(100m), await Get(client));
    }

    [Fact]
    public async Task Settings_AreKeptSeparatePerUser()
    {
        var owner = await SignedInClient();
        await Put(owner, 100m);
        var stranger = await SignedInClient();

        Assert.Equal(new SettingsDto(null), await Get(stranger));
        await Put(stranger, 5m);
        Assert.Equal(new SettingsDto(100m), await Get(owner));
    }
}
