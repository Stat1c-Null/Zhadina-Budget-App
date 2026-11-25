using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Zhadina.Api.Data;
using Zhadina.Api.Features.Accounts;
using Zhadina.Api.Infrastructure;

namespace Zhadina.Api.Features.Transactions;

// Every write runs in one database transaction: it locks the rows it touches, changes the balances and saves
// the transaction row together, so a balance can never change without its history row, or the reverse.
public static class TransactionEndpoints
{
    private const string NotYourAccount = "Choose one of your accounts.";
    private static readonly DateOnly EarliestDate = new(1900, 1, 1);

    public static IEndpointRouteBuilder MapTransactionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/transactions").RequireAuthorization();

        group.MapGet("/", List);
        group.MapPost("/", Create);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);

        return app;
    }

    private static async Task<Ok<List<TransactionResponse>>> List(ClaimsPrincipal principal, AppDbContext db)
    {
        var userId = principal.UserId();
        var transactions = await db.Transactions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .ThenBy(t => t.Id)
            .Select(t => new TransactionResponse(
                t.Id, t.Kind, t.AccountId, t.ToAccountId, t.Amount, t.Date, t.Category, t.Description))
            .ToListAsync();
        return TypedResults.Ok(transactions);
    }

    private static async Task<Results<Created<TransactionChangeResponse>, ValidationProblem>> Create(
        TransactionRequest request, ClaimsPrincipal principal, AppDbContext db)
    {
        if (Validate(request, out var errors) is not { } input)
            return TypedResults.ValidationProblem(errors);

        var userId = principal.UserId();
        await using var dbTransaction = await db.Database.BeginTransactionAsync();
        var accounts = await LockAccounts(db, userId, input.AccountIds());
        if ((CheckOwnership(input, accounts) ?? ApplyToBalances(accounts, Moves(input))) is { } problems)
            return TypedResults.ValidationProblem(problems);

        var transaction = new Transaction { UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
        input.CopyTo(transaction);
        db.Transactions.Add(transaction);
        await db.SaveChangesAsync();
        await dbTransaction.CommitAsync();

        return TypedResults.Created(
            $"/api/transactions/{transaction.Id}",
            new TransactionChangeResponse(ToResponse(transaction), AccountResponses(accounts)));
    }

    private static async Task<Results<Ok<TransactionChangeResponse>, NotFound, ValidationProblem>> Update(
        Guid id, TransactionRequest request, ClaimsPrincipal principal, AppDbContext db)
    {
        if (Validate(request, out var errors) is not { } input)
            return TypedResults.ValidationProblem(errors);

        var userId = principal.UserId();
        await using var dbTransaction = await db.Database.BeginTransactionAsync();
        if (await LockTransaction(db, userId, id) is not { } transaction)
            return TypedResults.NotFound();

        // Transfers and expenses or income use different modals, with different fields.
        if ((transaction.Kind == TransactionKind.Transfer) != (input.Kind == TransactionKind.Transfer))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["kind"] = ["A transfer can't become an expense or income, or the other way round. " +
                            "Delete it and add a new one."],
            });
        }

        var accounts = await LockAccounts(db, userId, AccountIds(transaction).Concat(input.AccountIds()));
        // Undo what the old version did, then apply the new one.
        var moves = Moves(transaction).Select(m => (m.AccountId, -m.Money)).Concat(Moves(input));
        if ((CheckOwnership(input, accounts) ?? ApplyToBalances(accounts, moves)) is { } problems)
            return TypedResults.ValidationProblem(problems);

        input.CopyTo(transaction);
        await db.SaveChangesAsync();
        await dbTransaction.CommitAsync();

        return TypedResults.Ok(new TransactionChangeResponse(ToResponse(transaction), AccountResponses(accounts)));
    }

    private static async Task<Results<Ok<AccountsChangeResponse>, NotFound, ValidationProblem>> Delete(
        Guid id, ClaimsPrincipal principal, AppDbContext db)
    {
        var userId = principal.UserId();
        await using var dbTransaction = await db.Database.BeginTransactionAsync();
        if (await LockTransaction(db, userId, id) is not { } transaction)
            return TypedResults.NotFound();

        var accounts = await LockAccounts(db, userId, AccountIds(transaction));
        // Only fails if a balance was edited by hand to near the limit since; undoing would then overflow.
        if (ApplyToBalances(accounts, Moves(transaction).Select(m => (m.AccountId, -m.Money))) is { } problems)
            return TypedResults.ValidationProblem(problems);

        db.Transactions.Remove(transaction);
        await db.SaveChangesAsync();
        await dbTransaction.CommitAsync();

        return TypedResults.Ok(new AccountsChangeResponse(AccountResponses(accounts)));
    }

    // The row lock makes a second edit or delete of the same transaction wait, so it can't be undone twice.
    // Run with ToListAsync: composing on it would make EF wrap the SQL, FOR UPDATE included, in a subquery.
    private static async Task<Transaction?> LockTransaction(AppDbContext db, Guid userId, Guid id) =>
        (await db.Transactions
            .FromSql($"""SELECT * FROM "Transactions" WHERE "Id" = {id} AND "UserId" = {userId} FOR UPDATE""")
            .ToListAsync())
        .SingleOrDefault();

    // Locked in id order, the same order in every request, so two transfers in opposite directions
    // wait for each other instead of deadlocking. Another user's ids simply aren't found.
    private static async Task<Dictionary<Guid, Account>> LockAccounts(
        AppDbContext db, Guid userId, IEnumerable<Guid> ids)
    {
        var distinct = ids.Distinct().ToArray();
        var accounts = await db.Accounts
            .FromSql($"""
                SELECT * FROM "Accounts" WHERE "UserId" = {userId} AND "Id" = ANY({distinct})
                ORDER BY "Id" FOR UPDATE
                """)
            .ToListAsync();
        return accounts.ToDictionary(a => a.Id);
    }

    private static Dictionary<string, string[]>? CheckOwnership(ValidInput input, Dictionary<Guid, Account> accounts)
    {
        var errors = new Dictionary<string, string[]>();
        if (!accounts.ContainsKey(input.AccountId))
            errors["accountId"] = [NotYourAccount];
        if (input.ToAccountId is { } to && !accounts.ContainsKey(to))
            errors["toAccountId"] = [NotYourAccount];
        return errors.Count > 0 ? errors : null;
    }

    // Adds the moves to the balances, or changes nothing and returns an error if one would pass what
    // numeric(14,2) holds. The rows are locked, so reading, adding and saving can't lose another request's change.
    private static Dictionary<string, string[]>? ApplyToBalances(
        Dictionary<Guid, Account> accounts, IEnumerable<(Guid AccountId, decimal Money)> moves)
    {
        var changes = moves
            .GroupBy(m => m.AccountId)
            .Select(g => (Account: accounts[g.Key], Change: g.Sum(m => BalanceEffects.StoredChange(accounts[g.Key].Type, m.Money))))
            .ToList();

        if (changes.FirstOrDefault(c => Math.Abs(c.Account.Balance + c.Change) > Account.MaxBalance) is { Account: { } tooBig })
        {
            return new Dictionary<string, string[]>
            {
                ["amount"] = [$"This would take {tooBig.Name} past the largest balance allowed."],
            };
        }

        foreach (var (account, change) in changes)
            account.Balance += change;
        return null;
    }

    private static IEnumerable<(Guid AccountId, decimal Money)> Moves(Transaction t) =>
        BalanceEffects.MoneyMoved(t.Kind, t.AccountId, t.ToAccountId, t.Amount);

    private static IEnumerable<(Guid AccountId, decimal Money)> Moves(ValidInput i) =>
        BalanceEffects.MoneyMoved(i.Kind, i.AccountId, i.ToAccountId, i.Amount);

    private static IEnumerable<Guid> AccountIds(Transaction t) =>
        t.ToAccountId is { } to ? [t.AccountId, to] : [t.AccountId];

    private static TransactionResponse ToResponse(Transaction t) =>
        new(t.Id, t.Kind, t.AccountId, t.ToAccountId, t.Amount, t.Date, t.Category, t.Description);

    private static List<AccountResponse> AccountResponses(Dictionary<Guid, Account> accounts) =>
        accounts.Values.Select(AccountEndpoints.ToResponse).ToList();

    private static Guid? ParseId(string? value) => Guid.TryParse(value, out var id) ? id : null;

    // A request that passed validation, with every value parsed. Ownership is checked later, under the locks.
    private sealed record ValidInput(
        TransactionKind Kind,
        Guid AccountId,
        Guid? ToAccountId,
        decimal Amount,
        DateOnly Date,
        TransactionCategory? Category,
        string? Description)
    {
        public IEnumerable<Guid> AccountIds() => ToAccountId is { } to ? [AccountId, to] : [AccountId];

        public void CopyTo(Transaction transaction)
        {
            transaction.Kind = Kind;
            transaction.AccountId = AccountId;
            transaction.ToAccountId = ToAccountId;
            transaction.Amount = Amount;
            transaction.Date = Date;
            transaction.Category = Category;
            transaction.Description = Description;
        }
    }

    // Names are matched exactly, because Enum.TryParse also accepts numbers such as "0".
    private static ValidInput? Validate(TransactionRequest request, out Dictionary<string, string[]> errors)
    {
        errors = new Dictionary<string, string[]>();

        TransactionKind? kind = null;
        if (request.Kind is { } kindName && Enum.GetNames<TransactionKind>().Contains(kindName))
            kind = Enum.Parse<TransactionKind>(kindName);
        else
            errors["kind"] = ["Choose expense, income or transfer."];

        var accountId = ParseId(request.AccountId);
        if (string.IsNullOrWhiteSpace(request.AccountId))
            errors["accountId"] = ["Choose an account."];
        else if (accountId is null)
            errors["accountId"] = [NotYourAccount];

        Guid? toAccountId = null;
        if (kind == TransactionKind.Transfer)
        {
            if (string.IsNullOrWhiteSpace(request.ToAccountId))
                errors["toAccountId"] = ["Choose the account the money goes to."];
            else if (ParseId(request.ToAccountId) is not { } to)
                errors["toAccountId"] = [NotYourAccount];
            else if (to == accountId)
                errors["toAccountId"] = ["Choose a different account to transfer to."];
            else
                toAccountId = to;
        }
        else if (kind is not null && request.ToAccountId is not null)
        {
            errors["toAccountId"] = ["Only a transfer goes to a second account."];
        }

        if (request.Amount is not { } amount)
            errors["amount"] = ["Amount is required."];
        else if (amount <= 0)
            errors["amount"] = ["Amount must be more than zero."];
        else if (amount > Account.MaxBalance)
            errors["amount"] = ["Amount is too large."];
        else if (decimal.Round(amount, 2) != amount)
            errors["amount"] = ["Amount can't have more than 2 decimal places."];

        // One day past the server's UTC date, so someone ahead of UTC can still record their "today".
        DateOnly? date = null;
        var latest = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        if (string.IsNullOrWhiteSpace(request.Date))
            errors["date"] = ["Date is required."];
        else if (!DateOnly.TryParseExact(
                     request.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            errors["date"] = ["Enter a real date."];
        else if (parsed < EarliestDate || parsed > latest)
            errors["date"] = ["Choose a date between 1900 and today."];
        else
            date = parsed;

        TransactionCategory? category = null;
        if (request.Category is { } categoryName)
        {
            if (kind == TransactionKind.Transfer)
                errors["category"] = ["A transfer doesn't have a category."];
            else if (!Enum.GetNames<TransactionCategory>().Contains(categoryName))
                errors["category"] = ["Choose a valid category."];
            else if (kind is { } k && !TransactionCategories.IsAllowed(k, Enum.Parse<TransactionCategory>(categoryName)))
                errors["category"] = [k == TransactionKind.Income ? "Choose an income category." : "Choose an expense category."];
            else
                category = Enum.Parse<TransactionCategory>(categoryName);
        }

        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        if (description?.Length > Transaction.DescriptionMaxLength)
            errors["description"] = [$"Description can't be longer than {Transaction.DescriptionMaxLength} characters."];

        if (errors.Count > 0)
            return null;
        return new ValidInput(kind!.Value, accountId!.Value, toAccountId, request.Amount!.Value, date!.Value, category, description);
    }
}
