using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Zhadina.Api.Data;
using Zhadina.Api.Infrastructure;

namespace Zhadina.Api.Features.Accounts;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounts").RequireAuthorization();

        group.MapGet("/", List);
        group.MapPost("/", Create);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);

        return app;
    }

    private static async Task<Ok<List<AccountResponse>>> List(ClaimsPrincipal principal, AppDbContext db)
    {
        var userId = principal.UserId();
        var accounts = await db.Accounts
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new AccountResponse(a.Id, a.Name, a.Type, a.Balance, a.Purpose))
            .ToListAsync();
        return TypedResults.Ok(accounts);
    }

    private static async Task<Results<Created<AccountResponse>, ValidationProblem>> Create(
        AccountRequest request, ClaimsPrincipal principal, AppDbContext db)
    {
        if (Validate(request) is { } errors)
            return TypedResults.ValidationProblem(errors);

        var account = new Account
        {
            UserId = principal.UserId(),
            Name = request.Name!.Trim(),
            Type = Enum.Parse<AccountType>(request.Type!),
            Balance = request.Balance!.Value,
            Purpose = ParsePurpose(request.Purpose),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        return TypedResults.Created($"/api/accounts/{account.Id}", ToResponse(account));
    }

    private static async Task<Results<Ok<AccountResponse>, NotFound, ValidationProblem>> Update(
        Guid id, AccountRequest request, ClaimsPrincipal principal, AppDbContext db)
    {
        if (Validate(request) is { } errors)
            return TypedResults.ValidationProblem(errors);

        var account = await FindOwned(db, id, principal);
        if (account is null)
            return TypedResults.NotFound();

        account.Name = request.Name!.Trim();
        account.Type = Enum.Parse<AccountType>(request.Type!);
        account.Balance = request.Balance!.Value;
        account.Purpose = ParsePurpose(request.Purpose);
        await db.SaveChangesAsync();

        return TypedResults.Ok(ToResponse(account));
    }

    private static async Task<Results<NoContent, NotFound>> Delete(
        Guid id, ClaimsPrincipal principal, AppDbContext db)
    {
        var account = await FindOwned(db, id, principal);
        if (account is null)
            return TypedResults.NotFound();

        db.Accounts.Remove(account);
        await db.SaveChangesAsync();

        return TypedResults.NoContent();
    }

    // Another user's account answers exactly like a missing one, so ids can't be probed.
    private static Task<Account?> FindOwned(AppDbContext db, Guid id, ClaimsPrincipal principal)
    {
        var userId = principal.UserId();
        return db.Accounts.SingleOrDefaultAsync(a => a.Id == id && a.UserId == userId);
    }

    private static AccountResponse ToResponse(Account account) =>
        new(account.Id, account.Name, account.Type, account.Balance, account.Purpose);

    // Only called after Validate, so the purpose is either missing or an exact member name.
    private static AccountPurpose ParsePurpose(string? purpose) =>
        purpose is null ? AccountPurpose.Spending : Enum.Parse<AccountPurpose>(purpose);

    private static Dictionary<string, string[]>? Validate(AccountRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Name))
            errors["name"] = ["Name is required."];
        else if (request.Name.Trim().Length > Account.NameMaxLength)
            errors["name"] = [$"Name can't be longer than {Account.NameMaxLength} characters."];

        // Matched against the names because Enum.TryParse also accepts numbers such as "3".
        if (string.IsNullOrWhiteSpace(request.Type))
            errors["type"] = ["Account type is required."];
        else if (!Enum.GetNames<AccountType>().Contains(request.Type))
            errors["type"] = ["Choose a valid account type."];

        // Left out means Spending. Anything sent must be an exact name, for the same reason as the type.
        // A credit card's balance is money owed, so it can't also count as savings or an investment.
        if (request.Purpose is { } purpose)
        {
            if (!Enum.GetNames<AccountPurpose>().Contains(purpose))
                errors["purpose"] = ["Choose savings, investment or neither."];
            else if (request.Type == nameof(AccountType.CreditCard) && purpose != nameof(AccountPurpose.Spending))
                errors["purpose"] = ["A credit card is money owed, so it can't be savings or an investment."];
        }

        if (request.Balance is not { } balance)
            errors["balance"] = ["Balance is required."];
        else if (Math.Abs(balance) > Account.MaxBalance)
            errors["balance"] = ["Balance is too large."];
        else if (decimal.Round(balance, 2) != balance)
            errors["balance"] = ["Balance can't have more than 2 decimal places."];

        return errors.Count > 0 ? errors : null;
    }
}
