using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Zhadina.Api.Features.Auth;

namespace Zhadina.Api.Tests;

public class AuthEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string GoodPassword = "correct-horse-1";

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static Task<HttpResponseMessage> Post(HttpClient client, string path, string? email, string? password) =>
        client.PostAsJsonAsync(path, new CredentialsRequest(email, password));

    [Fact]
    public async Task SignUp_LogsTheUserIn_AndSignOutEndsTheSession()
    {
        var client = factory.CreateClient();
        var email = NewEmail();

        var signUp = await Post(client, "/api/auth/sign-up", email, GoodPassword);
        Assert.Equal(HttpStatusCode.Created, signUp.StatusCode);

        var me = await client.GetFromJsonAsync<UserResponse>("/api/auth/me");
        Assert.Equal(email, me!.Email);

        var signOut = await client.PostAsync("/api/auth/sign-out", null);
        Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);

        var afterSignOut = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, afterSignOut.StatusCode);
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_ReturnsUserAndSetsHardenedCookie()
    {
        var email = NewEmail();
        await Post(factory.CreateClient(), "/api/auth/sign-up", email, GoodPassword);

        var client = factory.CreateClient();
        var login = await Post(client, "/api/auth/login", email, GoodPassword);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(email, (await login.Content.ReadFromJsonAsync<UserResponse>())!.Email);

        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"), c => c.StartsWith("zhadina_auth="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Login_WrongPasswordAndUnknownEmail_GetTheSameGeneric401()
    {
        var client = factory.CreateClient();
        var email = NewEmail();
        await Post(client, "/api/auth/sign-up", email, GoodPassword);
        await client.PostAsync("/api/auth/sign-out", null);

        var wrongPassword = await Post(client, "/api/auth/login", email, "wrong-password-1");
        var unknownEmail = await Post(client, "/api/auth/login", NewEmail(), GoodPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        var a = await wrongPassword.Content.ReadFromJsonAsync<ProblemDetails>();
        var b = await unknownEmail.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Invalid email or password.", a!.Detail);
        Assert.Equal(a.Detail, b!.Detail);
    }

    [Fact]
    public async Task Login_LocksOutAfterFiveFailures()
    {
        var client = factory.CreateClient();
        var email = NewEmail();
        await Post(client, "/api/auth/sign-up", email, GoodPassword);

        for (var i = 0; i < 5; i++)
            await Post(client, "/api/auth/login", email, "wrong-password-1");

        // Even the correct password is refused while locked out.
        var locked = await Post(client, "/api/auth/login", email, GoodPassword);
        Assert.Equal((HttpStatusCode)StatusCodes.Status423Locked, locked.StatusCode);
    }

    [Fact]
    public async Task SignUp_DuplicateEmail_ReturnsSingleEmailError()
    {
        var email = NewEmail();
        await Post(factory.CreateClient(), "/api/auth/sign-up", email, GoodPassword);

        var duplicate = await Post(factory.CreateClient(), "/api/auth/sign-up", email, GoodPassword);

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        var problem = await duplicate.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Single(problem!.Errors["email"]);
        Assert.False(problem.Errors.ContainsKey("password"));
    }

    [Theory]
    [InlineData("short1")]          // too short
    [InlineData("no-digits-here")]  // no digit
    public async Task SignUp_WeakPassword_ReturnsPasswordError(string password)
    {
        var response = await Post(factory.CreateClient(), "/api/auth/sign-up", NewEmail(), password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.True(problem!.Errors.ContainsKey("password"));
    }

    [Theory]
    [InlineData("/api/auth/sign-up")]
    [InlineData("/api/auth/login")]
    public async Task MissingFields_Return400WithBothErrors(string path)
    {
        var response = await Post(factory.CreateClient(), path, null, null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.True(problem!.Errors.ContainsKey("email"));
        Assert.True(problem.Errors.ContainsKey("password"));
    }

    [Fact]
    public async Task Me_WithoutCookie_Returns401InsteadOfRedirect()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await factory.CreateClient().GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

public class AuthRateLimitTests(RateLimitedApiFactory factory) : IClassFixture<RateLimitedApiFactory>
{
    [Fact]
    public async Task Login_IsRateLimited()
    {
        var client = factory.CreateClient();
        var body = new CredentialsRequest("nobody@example.com", "whatever-1");

        await client.PostAsJsonAsync("/api/auth/login", body);
        await client.PostAsJsonAsync("/api/auth/login", body);
        var third = await client.PostAsJsonAsync("/api/auth/login", body);

        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }
}
