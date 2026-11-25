using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using Zhadina.Api.Features.Auth;

namespace Zhadina.Api.Tests;

public class TransactionEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string BasePath = "/api/transactions";
    private const string Day = "2026-11-01";

    // Declared here rather than reusing the API's types so these tests pin the JSON contract the frontend relies on.
    private record AccountDto(Guid Id, string Name, string Type, decimal Balance, string Purpose);

    private record TransactionDto(
        Guid Id, string Kind, Guid AccountId, Guid? ToAccountId, decimal Amount, string Date, string? Category,
        string? Description);

    private record ChangeDto(TransactionDto Transaction, List<AccountDto> Accounts);

    private record AccountsDto(List<AccountDto> Accounts);

    private async Task<HttpClient> SignedInClient()
    {
        var client = factory.CreateClient();
        var signUp = await client.PostAsJsonAsync(
            "/api/auth/sign-up",
            new CredentialsRequest($"user-{Guid.NewGuid():N}@example.com", "correct-horse-1"));
        signUp.EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<AccountDto> CreateAccount(HttpClient client, string name, decimal balance, string type = "Bank")
    {
        var response = await client.PostAsJsonAsync("/api/accounts", new { name, type, balance });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AccountDto>())!;
    }

    private static async Task<decimal> Balance(HttpClient client, Guid accountId) =>
        (await client.GetFromJsonAsync<List<AccountDto>>("/api/accounts"))!.Single(a => a.Id == accountId).Balance;

    private static async Task<List<TransactionDto>> List(HttpClient client) =>
        (await client.GetFromJsonAsync<List<TransactionDto>>(BasePath))!;

    private static object Entry(
        Guid accountId, decimal amount, string kind = "Expense", string date = Day, string? category = null,
        string? description = null) =>
        new { kind, accountId, amount, date, category, description };

    private static object Transfer(Guid source, Guid to, decimal amount, string date = Day, string? description = null) =>
        new { kind = "Transfer", accountId = source, toAccountId = to, amount, date, description };

    private static async Task<ChangeDto> Add(HttpClient client, object body)
    {
        var response = await client.PostAsJsonAsync(BasePath, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ChangeDto>())!;
    }

    private static async Task<ChangeDto> Edit(HttpClient client, Guid id, object body)
    {
        var response = await client.PutAsJsonAsync($"{BasePath}/{id}", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ChangeDto>())!;
    }

    private static async Task AssertFieldError(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal([field], problem!.Errors.Keys);
    }

    [Theory]
    [InlineData("GET", BasePath)]
    [InlineData("POST", BasePath)]
    [InlineData("PUT", BasePath + "/00000000-0000-0000-0000-000000000001")]
    [InlineData("DELETE", BasePath + "/00000000-0000-0000-0000-000000000001")]
    public async Task Endpoints_RequireSignIn(string method, string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "POST" or "PUT")
            request.Content = JsonContent.Create(Entry(Guid.NewGuid(), 1m));

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_IsEmptyForANewUser()
    {
        Assert.Empty(await List(await SignedInClient()));
    }

    // A credit card's balance is the amount owed, so its sign runs the other way: spending on it owes more.
    [Theory]
    [InlineData("Bank", "Expense", 100, 40, 60)]
    [InlineData("Bank", "Income", 100, 25, 125)]
    [InlineData("Cash", "Expense", 10, 40, -30)]
    [InlineData("CreditCard", "Expense", 500, 40, 540)]
    [InlineData("CreditCard", "Income", 500, 25, 475)]
    public async Task Create_ChangesTheBalance(string type, string kind, int start, int amount, int expected)
    {
        var client = await SignedInClient();
        var account = await CreateAccount(client, "Wallet", start, type);

        var change = await Add(client, Entry(account.Id, amount, kind));

        Assert.Equal(kind, change.Transaction.Kind);
        Assert.Equal(account.Id, change.Transaction.AccountId);
        Assert.Null(change.Transaction.ToAccountId);
        Assert.Equal(amount, change.Transaction.Amount);
        Assert.Equal(Day, change.Transaction.Date);
        Assert.Equal([account with { Balance = expected }], change.Accounts);
        Assert.Equal(expected, await Balance(client, account.Id));
        Assert.Equal([change.Transaction], await List(client));
    }

    // Net worth moves the same way whichever way round a credit card is involved.
    [Theory]
    [InlineData("Bank", "Bank", 1000, 200, 100, 900, 300)]
    [InlineData("Bank", "CreditCard", 1000, 500, 100, 900, 400)]
    [InlineData("CreditCard", "Bank", 500, 1000, 100, 600, 1100)]
    [InlineData("CreditCard", "CreditCard", 500, 300, 100, 600, 200)]
    public async Task Transfer_MovesMoneyBetweenAccounts(
        string fromType, string toType, int fromStart, int toStart, int amount, int fromExpected, int toExpected)
    {
        var client = await SignedInClient();
        var source = await CreateAccount(client, "From", fromStart, fromType);
        var to = await CreateAccount(client, "To", toStart, toType);

        var change = await Add(client, Transfer(source.Id, to.Id, amount));

        Assert.Equal("Transfer", change.Transaction.Kind);
        Assert.Equal(source.Id, change.Transaction.AccountId);
        Assert.Equal(to.Id, change.Transaction.ToAccountId);
        Assert.Null(change.Transaction.Category);
        Assert.Equal(2, change.Accounts.Count);
        Assert.Contains(source with { Balance = fromExpected }, change.Accounts);
        Assert.Contains(to with { Balance = toExpected }, change.Accounts);
        Assert.Equal(fromExpected, await Balance(client, source.Id));
        Assert.Equal(toExpected, await Balance(client, to.Id));
    }

    [Fact]
    public async Task Create_StoresTheCategory_AndTrimsTheDescription()
    {
        var client = await SignedInClient();
        var account = await CreateAccount(client, "Checking", 100m);

        var groceries = await Add(client, Entry(account.Id, 12.34m, category: "Groceries", description: "  Market  "));
        var salary = await Add(client, Entry(account.Id, 1000m, "Income", category: "Salary", description: "   "));

        Assert.Equal("Groceries", groceries.Transaction.Category);
        Assert.Equal("Market", groceries.Transaction.Description);
        Assert.Equal("Salary", salary.Transaction.Category);
        // Blank is stored as no description.
        Assert.Null(salary.Transaction.Description);
    }

    [Fact]
    public async Task List_IsNewestFirst_ByDateThenByWhenItWasAdded()
    {
        var client = await SignedInClient();
        var account = await CreateAccount(client, "Checking", 100m);

        var earlyFirst = await Add(client, Entry(account.Id, 1m, date: "2026-11-02"));
        var late = await Add(client, Entry(account.Id, 2m, date: "2026-11-03"));
        var earlySecond = await Add(client, Entry(account.Id, 3m, date: "2026-11-02"));

        Assert.Equal(
            [late.Transaction, earlySecond.Transaction, earlyFirst.Transaction],
            await List(client));
    }

    [Theory]
    [InlineData("kind", null)]
    [InlineData("kind", "\"Spend\"")]
    [InlineData("kind", "\"0\"")]
    [InlineData("accountId", null)]
    [InlineData("accountId", "\"\"")]
    [InlineData("accountId", "\"not-a-guid\"")]
    [InlineData("accountId", "\"7d0b6c1e-3a52-4d84-9a52-0d6f6f6f6f6f\"")]
    [InlineData("amount", null)]
    [InlineData("amount", "0")]
    [InlineData("amount", "-5")]
    [InlineData("amount", "1.005")]
    [InlineData("amount", "1000000000000")]
    [InlineData("date", null)]
    [InlineData("date", "\"11/01/2026\"")]
    [InlineData("date", "\"2026-02-30\"")]
    [InlineData("date", "\"1899-12-31\"")]
    [InlineData("category", "\"Rent\"")]
    [InlineData("category", "\"\"")]
    [InlineData("category", "\"Salary\"")]
    public async Task Create_InvalidInput_ReturnsAFieldError_AndChangesNothing(string field, string? json)
    {
        var client = await SignedInClient();
        var account = await CreateAccount(client, "Checking", 100m);
        var body = new JsonObject
        {
            ["kind"] = "Expense",
            ["accountId"] = account.Id.ToString(),
            ["amount"] = 10m,
            ["date"] = Day,
        };
        if (json is null)
            body.Remove(field);
        else
            body[field] = JsonNode.Parse(json);

        var response = await client.PostAsJsonAsync(BasePath, body);

        await AssertFieldError(response, field);
        Assert.Equal(100m, await Balance(client, account.Id));
        Assert.Empty(await List(client));
    }

    [Fact]
    public async Task Create_ExpenseCategoryOnIncome_ReturnsAFieldError()
    {
        var client = await SignedInClient();
        var account = await CreateAccount(client, "Checking", 100m);

        var response = await client.PostAsJsonAsync(BasePath, Entry(account.Id, 10m, "Income", category: "Groceries"));

        await AssertFieldError(response, "category");
    }

    [Fact]
    public async Task Create_DescriptionUpToTheLimit_IsAccepted_AndOverItIsAFieldError()
    {
        var client = await SignedInClient();
        var account = await CreateAccount(client, "Checking", 100m);

        await Add(client, Entry(account.Id, 1m, description: new string('a', 200)));
        var response = await client.PostAsJsonAsync(BasePath, Entry(account.Id, 1m, description: new string('a', 201)));

        await AssertFieldError(response, "description");
    }

    // One day of slack lets someone ahead of UTC record their "today".
    [Fact]
    public async Task Create_AcceptsTomorrowInUtc_ButNotTheDayAfter()
    {
        var client = await SignedInClient();
        var account = await CreateAccount(client, "Checking", 100m);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await Add(client, Entry(account.Id, 1m, date: today.AddDays(1).ToString("yyyy-MM-dd")));
        var response = await client.PostAsJsonAsync(
            BasePath, Entry(account.Id, 1m, date: today.AddDays(2).ToString("yyyy-MM-dd")));

        await AssertFieldError(response, "date");
    }

    [Fact]
    public async Task Create_OnAnotherUsersAccount_ReturnsAnAccountError_AndChangesNothing()
    {
        var owner = await SignedInClient();
        var account = await CreateAccount(owner, "Private", 100m);
        var stranger = await SignedInClient();

        var response = await stranger.PostAsJsonAsync(BasePath, Entry(account.Id, 10m));

        await AssertFieldError(response, "accountId");
        Assert.Equal(100m, await Balance(owner, account.Id));
        Assert.Empty(await List(owner));
    }

    [Fact]
    public async Task Create_ExpenseWithASecondAccount_ReturnsAFieldError()
    {
        var client = await SignedInClient();
        var source = await CreateAccount(client, "Checking", 100m);
        var to = await CreateAccount(client, "Savings", 0m);

        var response = await client.PostAsJsonAsync(
            BasePath, new { kind = "Expense", accountId = source.Id, toAccountId = to.Id, amount = 1m, date = Day });

        await AssertFieldError(response, "toAccountId");
    }

    public enum BadTransfer { NoDestination, SameAccount, StrangersAccount, WithCategory }

    [Theory]
    [InlineData(BadTransfer.NoDestination, "toAccountId")]
    [InlineData(BadTransfer.SameAccount, "toAccountId")]
    [InlineData(BadTransfer.StrangersAccount, "toAccountId")]
    [InlineData(BadTransfer.WithCategory, "category")]
    public async Task Create_InvalidTransfer_ReturnsAFieldError_AndChangesNothing(BadTransfer bad, string field)
    {
        var client = await SignedInClient();
        var source = await CreateAccount(client, "Checking", 100m);
        var to = await CreateAccount(client, "Savings", 0m);
        var strangers = await CreateAccount(await SignedInClient(), "Private", 0m);
        object body = bad switch
        {
            BadTransfer.NoDestination => new { kind = "Transfer", accountId = source.Id, amount = 10m, date = Day },
            BadTransfer.SameAccount => Transfer(source.Id, source.Id, 10m),
            BadTransfer.StrangersAccount => Transfer(source.Id, strangers.Id, 10m),
            _ => new { kind = "Transfer", accountId = source.Id, toAccountId = to.Id, amount = 10m, date = Day, category = "Other" },
        };

        var response = await client.PostAsJsonAsync(BasePath, body);

        await AssertFieldError(response, field);
        Assert.Equal(100m, await Balance(client, source.Id));
        Assert.Equal(0m, await Balance(client, to.Id));
    }

    // numeric(14,2) can't hold more, so this is a field error rather than a database overflow.
    [Theory]
    [InlineData("Income", 999_999_999_999.99)]
    [InlineData("Expense", -999_999_999_999.99)]
    public async Task Create_PastTheLargestBalance_ReturnsAnAmountError_AndChangesNothing(string kind, double start)
    {
        var client = await SignedInClient();
        var account = await CreateAccount(client, "Huge", (decimal)start);

        var response = await client.PostAsJsonAsync(BasePath, Entry(account.Id, 0.01m, kind));

        await AssertFieldError(response, "amount");
        Assert.Equal((decimal)start, await Balance(client, account.Id));
        Assert.Empty(await List(client));
    }

    [Fact]
    public async Task Update_ChangesTheAmount_AndReappliesTheEffect()
    {
        var client = await SignedInClient();
        var account = await CreateAccount(client, "Checking", 100m);
        var added = await Add(client, Entry(account.Id, 40m));

        var change = await Edit(client, added.Transaction.Id, Entry(account.Id, 45m));

        Assert.Equal(45m, change.Transaction.Amount);
        Assert.Equal(added.Transaction.Id, change.Transaction.Id);
        Assert.Equal([account with { Balance = 55m }], change.Accounts);
        Assert.Equal(55m, await Balance(client, account.Id));
    }

    [Fact]
    public async Task Update_MovesTheTransactionToAnotherAccount()
    {
        var client = await SignedInClient();
        var first = await CreateAccount(client, "Checking", 100m);
        var second = await CreateAccount(client, "Card", 500m, "CreditCard");
        var added = await Add(client, Entry(first.Id, 40m));

        var change = await Edit(client, added.Transaction.Id, Entry(second.Id, 40m));

        Assert.Contains(first with { Balance = 100m }, change.Accounts);
        Assert.Contains(second with { Balance = 540m }, change.Accounts);
        Assert.Equal(100m, await Balance(client, first.Id));
        Assert.Equal(540m, await Balance(client, second.Id));
    }

    [Fact]
    public async Task Update_SwitchesAnExpenseToIncome()
    {
        var client = await SignedInClient();
        var account = await CreateAccount(client, "Checking", 100m);
        var added = await Add(client, Entry(account.Id, 40m, category: "Groceries"));

        var change = await Edit(client, added.Transaction.Id, Entry(account.Id, 40m, "Income", category: "Refunds"));

        Assert.Equal("Income", change.Transaction.Kind);
        Assert.Equal("Refunds", change.Transaction.Category);
        Assert.Equal(140m, await Balance(client, account.Id));
    }

    [Fact]
    public async Task Update_ChangesWhereATransferGoes()
    {
        var client = await SignedInClient();
        var source = await CreateAccount(client, "Checking", 1000m);
        var oldTo = await CreateAccount(client, "Savings", 0m);
        var newTo = await CreateAccount(client, "Brokerage", 0m, "Investment");
        var added = await Add(client, Transfer(source.Id, oldTo.Id, 100m));

        var change = await Edit(client, added.Transaction.Id, Transfer(source.Id, newTo.Id, 100m));

        Assert.Equal(newTo.Id, change.Transaction.ToAccountId);
        Assert.Equal(900m, await Balance(client, source.Id));
        Assert.Equal(0m, await Balance(client, oldTo.Id));
        Assert.Equal(100m, await Balance(client, newTo.Id));
    }

    [Fact]
    public async Task Update_OnlyTheDescriptionAndDate_KeepsTheBalance()
    {
        var client = await SignedInClient();
        var account = await CreateAccount(client, "Checking", 100m);
        var added = await Add(client, Entry(account.Id, 40m, description: "Old"));

        var change = await Edit(client, added.Transaction.Id, Entry(account.Id, 40m, date: "2026-10-31", description: "New"));

        Assert.Equal("New", change.Transaction.Description);
        Assert.Equal("2026-10-31", change.Transaction.Date);
        Assert.Equal(60m, await Balance(client, account.Id));
        Assert.Equal([change.Transaction], await List(client));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Update_BetweenATransferAndAnythingElse_ReturnsAKindError_AndChangesNothing(bool startAsTransfer)
    {
        var client = await SignedInClient();
        var source = await CreateAccount(client, "Checking", 100m);
        var to = await CreateAccount(client, "Savings", 0m);
        var added = await Add(client, startAsTransfer ? Transfer(source.Id, to.Id, 10m) : Entry(source.Id, 10m));

        var response = await client.PutAsJsonAsync(
            $"{BasePath}/{added.Transaction.Id}", startAsTransfer ? Entry(source.Id, 10m) : Transfer(source.Id, to.Id, 10m));

        await AssertFieldError(response, "kind");
        Assert.Equal([added.Transaction], await List(client));
        Assert.Equal(90m, await Balance(client, source.Id));
    }

    [Fact]
    public async Task Update_InvalidInput_ReturnsAFieldError_AndKeepsTheTransaction()
    {
        var client = await SignedInClient();
        var account = await CreateAccount(client, "Checking", 100m);
        var added = await Add(client, Entry(account.Id, 40m));

        var response = await client.PutAsJsonAsync($"{BasePath}/{added.Transaction.Id}", Entry(account.Id, 0m));

        await AssertFieldError(response, "amount");
        Assert.Equal([added.Transaction], await List(client));
        Assert.Equal(60m, await Balance(client, account.Id));
    }

    [Fact]
    public async Task Update_OntoAnotherUsersAccount_ReturnsAnAccountError_AndChangesNothing()
    {
        var client = await SignedInClient();
        var account = await CreateAccount(client, "Checking", 100m);
        var added = await Add(client, Entry(account.Id, 40m));
        var owner = await SignedInClient();
        var strangers = await CreateAccount(owner, "Private", 100m);

        var response = await client.PutAsJsonAsync($"{BasePath}/{added.Transaction.Id}", Entry(strangers.Id, 40m));

        await AssertFieldError(response, "accountId");
        Assert.Equal(60m, await Balance(client, account.Id));
        Assert.Equal(100m, await Balance(owner, strangers.Id));
    }

    [Fact]
    public async Task Update_UnknownTransaction_Returns404()
    {
        var client = await SignedInClient();
        var account = await CreateAccount(client, "Checking", 100m);

        var response = await client.PutAsJsonAsync($"{BasePath}/{Guid.NewGuid()}", Entry(account.Id, 1m));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_AnotherUsersTransaction_Returns404_AndChangesNothing()
    {
        var owner = await SignedInClient();
        var account = await CreateAccount(owner, "Private", 100m);
        var added = await Add(owner, Entry(account.Id, 40m));
        var stranger = await SignedInClient();
        var strangersAccount = await CreateAccount(stranger, "Mine", 100m);

        var response = await stranger.PutAsJsonAsync(
            $"{BasePath}/{added.Transaction.Id}", Entry(strangersAccount.Id, 1m));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal([added.Transaction], await List(owner));
        Assert.Equal(60m, await Balance(owner, account.Id));
        Assert.Equal(100m, await Balance(stranger, strangersAccount.Id));
    }

    [Fact]
    public async Task Delete_UndoesAnExpense()
    {
        var client = await SignedInClient();
        var account = await CreateAccount(client, "Checking", 100m);
        var added = await Add(client, Entry(account.Id, 40m));

        var response = await client.DeleteAsync($"{BasePath}/{added.Transaction.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<AccountsDto>())!;
        Assert.Equal([account], body.Accounts);
        Assert.Equal(100m, await Balance(client, account.Id));
        Assert.Empty(await List(client));
    }

    [Fact]
    public async Task Delete_UndoesATransferOnBothAccounts()
    {
        var client = await SignedInClient();
        var source = await CreateAccount(client, "Checking", 1000m);
        var to = await CreateAccount(client, "Card", 500m, "CreditCard");
        var added = await Add(client, Transfer(source.Id, to.Id, 100m));

        var response = await client.DeleteAsync($"{BasePath}/{added.Transaction.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1000m, await Balance(client, source.Id));
        Assert.Equal(500m, await Balance(client, to.Id));
    }

    [Fact]
    public async Task Delete_UnknownTransaction_Returns404()
    {
        var response = await (await SignedInClient()).DeleteAsync($"{BasePath}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AnotherUsersTransaction_Returns404_AndKeepsIt()
    {
        var owner = await SignedInClient();
        var account = await CreateAccount(owner, "Private", 100m);
        var added = await Add(owner, Entry(account.Id, 40m));

        var response = await (await SignedInClient()).DeleteAsync($"{BasePath}/{added.Transaction.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal([added.Transaction], await List(owner));
        Assert.Equal(60m, await Balance(owner, account.Id));
    }

    [Fact]
    public async Task List_NeverShowsAnotherUsersTransactions()
    {
        var owner = await SignedInClient();
        await Add(owner, Entry((await CreateAccount(owner, "Private", 100m)).Id, 1m));

        Assert.Empty(await List(await SignedInClient()));
    }

    // The money in a transfer really moved, so the other account keeps it.
    [Fact]
    public async Task DeletingAnAccount_DeletesItsTransactions_AndTheOtherAccountKeepsItsBalance()
    {
        var client = await SignedInClient();
        var removed = await CreateAccount(client, "Old", 1000m);
        var kept = await CreateAccount(client, "Kept", 0m);
        await Add(client, Entry(removed.Id, 10m));
        await Add(client, Transfer(removed.Id, kept.Id, 100m));
        await Add(client, Transfer(kept.Id, removed.Id, 30m));
        var unrelated = await Add(client, Entry(kept.Id, 5m));

        var response = await client.DeleteAsync($"/api/accounts/{removed.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal([unrelated.Transaction], await List(client));
        Assert.Equal(65m, await Balance(client, kept.Id));
    }

    // Each balance change is applied under a row lock, so saves at the same moment can't overwrite each other.
    [Fact]
    public async Task ExpensesSavedAtTheSameTime_AllCount()
    {
        var client = await SignedInClient();
        var account = await CreateAccount(client, "Checking", 100m);

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 20).Select(_ => client.PostAsJsonAsync(BasePath, Entry(account.Id, 1m))));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        Assert.Equal(80m, await Balance(client, account.Id));
        Assert.Equal(20, (await List(client)).Count);
    }

    // Accounts are locked in id order, so opposite transfers at once wait for each other instead of deadlocking.
    [Fact]
    public async Task OppositeTransfersAtTheSameTime_AllSucceed()
    {
        var client = await SignedInClient();
        var first = await CreateAccount(client, "First", 100m);
        var second = await CreateAccount(client, "Second", 100m);

        var responses = await Task.WhenAll(Enumerable.Range(0, 20).Select(i => client.PostAsJsonAsync(
            BasePath, i % 2 == 0 ? Transfer(first.Id, second.Id, 1m) : Transfer(second.Id, first.Id, 1m))));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        Assert.Equal(100m, await Balance(client, first.Id));
        Assert.Equal(100m, await Balance(client, second.Id));
    }
}
