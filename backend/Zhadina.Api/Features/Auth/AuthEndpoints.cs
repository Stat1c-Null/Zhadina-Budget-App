using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Zhadina.Api.Data;

namespace Zhadina.Api.Features.Auth;

public static class AuthEndpoints
{
    public const string RateLimitPolicy = "auth";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/sign-up", SignUp).RequireRateLimiting(RateLimitPolicy);
        group.MapPost("/login", Login).RequireRateLimiting(RateLimitPolicy);
        group.MapPost("/sign-out", SignOut);
        group.MapGet("/me", Me).RequireAuthorization();

        return app;
    }

    private static async Task<Results<Created<UserResponse>, ValidationProblem>> SignUp(
        CredentialsRequest request, UserManager<AppUser> users, SignInManager<AppUser> signIn)
    {
        if (MissingFields(request) is { } missing)
            return TypedResults.ValidationProblem(missing);

        var email = request.Email!.Trim();
        var user = new AppUser { UserName = email, Email = email };
        var result = await users.CreateAsync(user, request.Password!);
        if (!result.Succeeded)
            return TypedResults.ValidationProblem(ToFieldErrors(result.Errors));

        await signIn.SignInAsync(user, isPersistent: true);
        return TypedResults.Created("/api/auth/me", new UserResponse(user.Id, user.Email!));
    }

    private static async Task<Results<Ok<UserResponse>, ValidationProblem, ProblemHttpResult>> Login(
        CredentialsRequest request, UserManager<AppUser> users, SignInManager<AppUser> signIn)
    {
        if (MissingFields(request) is { } missing)
            return TypedResults.ValidationProblem(missing);

        var email = request.Email!.Trim();
        var result = await signIn.PasswordSignInAsync(email, request.Password!, isPersistent: true, lockoutOnFailure: true);

        if (result.IsLockedOut)
            return TypedResults.Problem(
                "Too many failed attempts. Try again in a few minutes.",
                statusCode: StatusCodes.Status423Locked);

        // Same message for unknown email and wrong password, so login doesn't reveal which accounts exist.
        if (!result.Succeeded)
            return TypedResults.Problem("Invalid email or password.", statusCode: StatusCodes.Status401Unauthorized);

        var user = await users.FindByNameAsync(email);
        return TypedResults.Ok(new UserResponse(user!.Id, user.Email!));
    }

    // Cookie sessions: signing out only expires the cookie, no database work.
    private static async Task<NoContent> SignOut(SignInManager<AppUser> signIn)
    {
        await signIn.SignOutAsync();
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<UserResponse>, UnauthorizedHttpResult>> Me(
        ClaimsPrincipal principal, UserManager<AppUser> users)
    {
        var user = await users.GetUserAsync(principal);
        return user is null
            ? TypedResults.Unauthorized()
            : TypedResults.Ok(new UserResponse(user.Id, user.Email!));
    }

    private static Dictionary<string, string[]>? MissingFields(CredentialsRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Email)) errors["email"] = ["Email is required."];
        if (string.IsNullOrEmpty(request.Password)) errors["password"] = ["Password is required."];
        return errors.Count > 0 ? errors : null;
    }

    // UserName == Email, so Identity reports duplicates twice; keep only the email-facing errors.
    private static Dictionary<string, string[]> ToFieldErrors(IEnumerable<IdentityError> errors) =>
        errors
            .Where(e => e.Code != nameof(IdentityErrorDescriber.DuplicateUserName))
            .GroupBy(e => e.Code.StartsWith("Password") ? "password" : "email")
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
}
