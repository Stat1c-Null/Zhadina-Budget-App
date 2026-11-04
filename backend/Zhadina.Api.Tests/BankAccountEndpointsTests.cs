using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using Zhadina.Api.Features.Auth;

namespace Zhadina.Api.Tests;

public class BankAccountEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string BasePath = "/api/bank-accounts";

    // Declared here rather than reusing the API's type so these tests pin the JSON contract the frontend relies on.
    private record AccountDto(Guid Id, string Name, decimal Balance);

    private async Task<HttpClient> SignedInClient()
    {
        var client = factory.CreateClient();
        var signUp = await client.PostAsJsonAsync(
            "/api/auth/sign-up",
            new CredentialsRequest($"user-{Guid.NewGuid():N}@example.com", "correct-horse-1"));
        signUp.EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<AccountDto> Create(HttpClient client, string name, decimal balance)
    {
        var response = await client.PostAsJsonAsync(BasePath, new { name, balance });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AccountDto>())!;
    }

    private static async Task<List<AccountDto>> List(HttpClient client) =>
        (await client.GetFromJsonAsync<List<AccountDto>>(BasePath))!;

    [Theory]
    [InlineData("GET", BasePath)]
    [InlineData("POST", BasePath)]
    [InlineData("PUT", BasePath + "/00000000-0000-0000-0000-000000000001")]
    [InlineData("DELETE", BasePath + "/00000000-0000-0000-0000-000000000001")]
    public async Task Endpoints_RequireSignIn(string method, string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "POST" or "PUT")
            request.Content = JsonContent.Create(new { name = "Checking", balance = 1m });

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_IsEmptyForANewUser()
    {
        Assert.Empty(await List(await SignedInClient()));
    }

    [Fact]
    public async Task Create_PersistsTheAccount_AndListReturnsItInCreationOrder()
    {
        var client = await SignedInClient();

        var first = await Create(client, "Checking", 1234.50m);
        var second = await Create(client, "Savings", 0m);

        Assert.Equal([first, second], await List(client));
        Assert.Equal("Checking", first.Name);
        Assert.Equal(1234.50m, first.Balance);
    }

    [Fact]
    public async Task Create_TrimsTheName()
    {
        var account = await Create(await SignedInClient(), "  Checking  ", 1m);

        Assert.Equal("Checking", account.Name);
    }

    [Fact]
    public async Task Create_AllowsAnOverdrawnBalance()
    {
        var account = await Create(await SignedInClient(), "Overdraft", -250.75m);

        Assert.Equal(-250.75m, account.Balance);
    }

    [Fact]
    public async Task Create_AcceptsTheLargestAllowedValues()
    {
        var client = await SignedInClient();
        var name = new string('x', 100);

        await Create(client, name, 999_999_999_999.99m);
        await Create(client, name, -999_999_999_999.99m);

        var balances = (await List(client)).Select(a => a.Balance);
        Assert.Equal([999_999_999_999.99m, -999_999_999_999.99m], balances);
    }

    [Theory]
    [InlineData(null, 10.0, "name")]
    [InlineData("   ", 10.0, "name")]
    [InlineData("Checking", null, "balance")]
    [InlineData("Checking", 12.345, "balance")]
    [InlineData("Checking", 1000000000000.0, "balance")]
    [InlineData("Checking", -1000000000000.0, "balance")]
    public async Task Create_InvalidInput_ReturnsAFieldError(string? name, double? balance, string field)
    {
        var client = await SignedInClient();

        var response = await client.PostAsJsonAsync(BasePath, new { name, balance });

        await AssertFieldError(response, field);
        Assert.Empty(await List(client));
    }

    [Fact]
    public async Task Create_NameOverTheLimit_ReturnsAFieldError()
    {
        var response = await (await SignedInClient()).PostAsJsonAsync(
            BasePath, new { name = new string('x', 101), balance = 1m });

        await AssertFieldError(response, "name");
    }

    [Fact]
    public async Task Update_ChangesNameAndBalance()
    {
        var client = await SignedInClient();
        var account = await Create(client, "Checking", 10m);

        var response = await client.PutAsJsonAsync(
            $"{BasePath}/{account.Id}", new { name = " Everyday ", balance = 99.99m });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var expected = new AccountDto(account.Id, "Everyday", 99.99m);
        Assert.Equal(expected, await response.Content.ReadFromJsonAsync<AccountDto>());
        Assert.Equal([expected], await List(client));
    }

    [Fact]
    public async Task Update_InvalidInput_ReturnsAFieldError_AndKeepsTheOldValues()
    {
        var client = await SignedInClient();
        var account = await Create(client, "Checking", 10m);

        var response = await client.PutAsJsonAsync($"{BasePath}/{account.Id}", new { name = "", balance = 5m });

        await AssertFieldError(response, "name");
        Assert.Equal([account], await List(client));
    }

    [Fact]
    public async Task Update_UnknownAccount_Returns404()
    {
        var response = await (await SignedInClient()).PutAsJsonAsync(
            $"{BasePath}/{Guid.NewGuid()}", new { name = "Checking", balance = 1m });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesTheAccount()
    {
        var client = await SignedInClient();
        var removed = await Create(client, "Old", 1m);
        var kept = await Create(client, "Kept", 2m);

        var response = await client.DeleteAsync($"{BasePath}/{removed.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal([kept], await List(client));
    }

    [Fact]
    public async Task Delete_UnknownAccount_Returns404()
    {
        var response = await (await SignedInClient()).DeleteAsync($"{BasePath}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_NeverShowsAnotherUsersAccounts()
    {
        var owner = await SignedInClient();
        await Create(owner, "Private", 1m);

        Assert.Empty(await List(await SignedInClient()));
    }

    [Fact]
    public async Task Update_AnotherUsersAccount_Returns404_AndChangesNothing()
    {
        var owner = await SignedInClient();
        var account = await Create(owner, "Private", 1m);
        var stranger = await SignedInClient();

        var response = await stranger.PutAsJsonAsync(
            $"{BasePath}/{account.Id}", new { name = "Hacked", balance = 0m });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal([account], await List(owner));
    }

    [Fact]
    public async Task Delete_AnotherUsersAccount_Returns404_AndKeepsIt()
    {
        var owner = await SignedInClient();
        var account = await Create(owner, "Private", 1m);
        var stranger = await SignedInClient();

        var response = await stranger.DeleteAsync($"{BasePath}/{account.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal([account], await List(owner));
    }

    private static async Task AssertFieldError(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal([field], problem!.Errors.Keys);
    }
}
