using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;
using Zhadina.Api.Data;

namespace Zhadina.Api.Tests;

// Besides creating the table, this migration makes every credit card's balance the amount owed (never negative),
// because transactions now add to and subtract from it. Values are read with SQL, as in the other migration tests.
public class AddTransactionsMigrationTests : IAsyncLifetime
{
    private const string BeforeTransactions = "20261119013526_AddAccountPurpose";

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid NegativeCard = Guid.NewGuid();
    private static readonly Guid PositiveCard = Guid.NewGuid();
    private static readonly Guid OverdrawnBank = Guid.NewGuid();
    private static readonly DateTimeOffset CreatedAt = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    private readonly string _connectionString =
        $"{Environment.GetEnvironmentVariable("ZHADINA_TEST_PG") ?? "Host=localhost;Port=5432;Username=postgres"};" +
        $"Database=zhadina_test_{Guid.NewGuid():N}";

    private AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_connectionString).Options);

    private static Task MigrateTo(AppDbContext db, string? migration = null) =>
        db.GetService<IMigrator>().MigrateAsync(migration);

    private static Task<decimal> StoredBalance(AppDbContext db, Guid accountId) =>
        db.Database.SqlQuery<decimal>($"""SELECT "Balance" AS "Value" FROM "Accounts" WHERE "Id" = {accountId}""")
            .SingleAsync();

    private static Task<int> TransactionsTables(AppDbContext db) =>
        db.Database.SqlQuery<int>($"""
            SELECT count(*)::int AS "Value" FROM information_schema.tables WHERE table_name = 'Transactions'
            """).SingleAsync();

    // A database as it looks before this migration: a card typed as negative (ADR-009 allowed either sign),
    // a card typed as positive, and an overdrawn bank account.
    public async Task InitializeAsync()
    {
        await using var db = NewContext();
        await MigrateTo(db, BeforeTransactions);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO "AspNetUsers"
                ("Id", "AccessFailedCount", "EmailConfirmed", "LockoutEnabled", "PhoneNumberConfirmed", "TwoFactorEnabled")
            VALUES ({UserId}, 0, false, false, false, false)
            """);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO "Accounts" ("Id", "UserId", "Name", "Type", "Balance", "CreatedAt", "Purpose") VALUES
                ({NegativeCard}, {UserId}, 'Visa', 'CreditCard', -500.25, {CreatedAt}, 'Spending'),
                ({PositiveCard}, {UserId}, 'Amex', 'CreditCard', 300.00, {CreatedAt}, 'Spending'),
                ({OverdrawnBank}, {UserId}, 'Checking', 'Bank', -80.50, {CreatedAt}, 'Spending')
            """);
    }

    public async Task DisposeAsync()
    {
        await using var db = NewContext();
        await db.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task Up_MakesNegativeCreditCardsPositive_AndLeavesEverythingElse()
    {
        await using var db = NewContext();

        await MigrateTo(db);

        Assert.Equal(500.25m, await StoredBalance(db, NegativeCard));
        Assert.Equal(300.00m, await StoredBalance(db, PositiveCard));
        // Only cards change: an overdrawn bank account really is negative.
        Assert.Equal(-80.50m, await StoredBalance(db, OverdrawnBank));
    }

    [Fact]
    public async Task Up_CreatesTheTransactionsTable_WithItsChecks()
    {
        await using var db = NewContext();

        await MigrateTo(db);

        Assert.Equal(1, await TransactionsTables(db));
        var checks = await db.Database.SqlQuery<string>($"""
            SELECT conname::text AS "Value" FROM pg_constraint
            WHERE conrelid = '"Transactions"'::regclass AND contype = 'c' ORDER BY conname
            """).ToListAsync();
        Assert.Equal(
            [
                "CK_Transactions_AmountPositive",
                "CK_Transactions_NoCategoryOnTransfers",
                "CK_Transactions_ToAccountOnlyOnTransfers",
                "CK_Transactions_TransferBetweenTwoAccounts",
            ],
            checks);

        // A transfer from an account to itself is refused by the database, not only by the API.
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlAsync($"""
            INSERT INTO "Transactions" ("Id", "UserId", "Kind", "AccountId", "ToAccountId", "Amount", "Date", "CreatedAt")
            VALUES ({Guid.NewGuid()}, {UserId}, 'Transfer', {OverdrawnBank}, {OverdrawnBank}, 10, '2026-11-23', {CreatedAt})
            """));
        Assert.Equal("CK_Transactions_TransferBetweenTwoAccounts", error.ConstraintName);
    }

    [Fact]
    public async Task Down_DropsTheTable_KeepsTheAccounts_AndUpWorksAgain()
    {
        await using var db = NewContext();
        await MigrateTo(db);

        await MigrateTo(db, BeforeTransactions);

        Assert.Equal(0, await TransactionsTables(db));
        // The card's old sign can't be restored, and doesn't need to be: under ADR-009 it carried no meaning.
        Assert.Equal(500.25m, await StoredBalance(db, NegativeCard));
        Assert.Equal(-80.50m, await StoredBalance(db, OverdrawnBank));

        await MigrateTo(db);
        Assert.Equal(1, await TransactionsTables(db));
    }
}
