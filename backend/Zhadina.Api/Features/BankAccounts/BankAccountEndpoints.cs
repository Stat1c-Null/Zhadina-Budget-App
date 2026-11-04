using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Zhadina.Api.Data;

namespace Zhadina.Api.Features.BankAccounts;

public static class BankAccountEndpoints
{
    public static IEndpointRouteBuilder MapBankAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/bank-accounts").RequireAuthorization();

        group.MapGet("/", List);
        group.MapPost("/", Create);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);

        return app;
    }

    private static async Task<Ok<List<BankAccountResponse>>> List(ClaimsPrincipal principal, AppDbContext db)
    {
        var userId = UserId(principal);
        var accounts = await db.BankAccounts
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new BankAccountResponse(a.Id, a.Name, a.Balance))
            .ToListAsync();
        return TypedResults.Ok(accounts);
    }

    private static async Task<Results<Created<BankAccountResponse>, ValidationProblem>> Create(
        BankAccountRequest request, ClaimsPrincipal principal, AppDbContext db)
    {
        if (Validate(request) is { } errors)
            return TypedResults.ValidationProblem(errors);

        var account = new BankAccount
        {
            UserId = UserId(principal),
            Name = request.Name!.Trim(),
            Balance = request.Balance!.Value,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.BankAccounts.Add(account);
        await db.SaveChangesAsync();

        return TypedResults.Created($"/api/bank-accounts/{account.Id}", ToResponse(account));
    }

    private static async Task<Results<Ok<BankAccountResponse>, NotFound, ValidationProblem>> Update(
        Guid id, BankAccountRequest request, ClaimsPrincipal principal, AppDbContext db)
    {
        if (Validate(request) is { } errors)
            return TypedResults.ValidationProblem(errors);

        var account = await FindOwned(db, id, principal);
        if (account is null)
            return TypedResults.NotFound();

        account.Name = request.Name!.Trim();
        account.Balance = request.Balance!.Value;
        await db.SaveChangesAsync();

        return TypedResults.Ok(ToResponse(account));
    }

    private static async Task<Results<NoContent, NotFound>> Delete(
        Guid id, ClaimsPrincipal principal, AppDbContext db)
    {
        var account = await FindOwned(db, id, principal);
        if (account is null)
            return TypedResults.NotFound();

        db.BankAccounts.Remove(account);
        await db.SaveChangesAsync();

        return TypedResults.NoContent();
    }

    // Another user's account answers exactly like a missing one, so ids can't be probed.
    private static Task<BankAccount?> FindOwned(AppDbContext db, Guid id, ClaimsPrincipal principal)
    {
        var userId = UserId(principal);
        return db.BankAccounts.SingleOrDefaultAsync(a => a.Id == id && a.UserId == userId);
    }

    // RequireAuthorization on the group guarantees a signed-in principal, which always carries this claim.
    private static Guid UserId(ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static BankAccountResponse ToResponse(BankAccount account) =>
        new(account.Id, account.Name, account.Balance);

    private static Dictionary<string, string[]>? Validate(BankAccountRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Name))
            errors["name"] = ["Name is required."];
        else if (request.Name.Trim().Length > BankAccount.NameMaxLength)
            errors["name"] = [$"Name can't be longer than {BankAccount.NameMaxLength} characters."];

        if (request.Balance is not { } balance)
            errors["balance"] = ["Balance is required."];
        else if (Math.Abs(balance) > BankAccount.MaxBalance)
            errors["balance"] = ["Balance is too large."];
        else if (decimal.Round(balance, 2) != balance)
            errors["balance"] = ["Balance can't have more than 2 decimal places."];

        return errors.Count > 0 ? errors : null;
    }
}
