using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;
using Zhadina.Api.Data;

namespace Zhadina.Api.Tests;

// EF scaffolded the new column with an empty-string default, which isn't a purpose,
// so these check that accounts saved before the migration are stored as Spending.
// The stored text is read with SQL: EF's enum converter reads any unknown text as the first member,
// which is Spending, so reading through db.Accounts can't tell "" from "Spending".
public class AddAccountPurposeMigrationTests : IAsyncLifetime
{
    private const string BeforePurpose = "20261119002633_AddUserSettings";

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly DateTimeOffset CreatedAt = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly string _connectionString =
        $"{Environment.GetEnvironmentVariable("ZHADINA_TEST_PG") ?? "Host=localhost;Port=5432;Username=postgres"};" +
        $"Database=zhadina_test_{Guid.NewGuid():N}";

    private AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_connectionString).Options);

    private static Task MigrateTo(AppDbContext db, string? migration = null) =>
        db.GetService<IMigrator>().MigrateAsync(migration);

    private static Task<string> StoredPurpose(AppDbContext db) =>
        db.Database.SqlQuery<string>($"""SELECT "Purpose" AS "Value" FROM "Accounts" """).SingleAsync();

    // A database as it looks before this migration: one user with one account.
    public async Task InitializeAsync()
    {
        await using var db = NewContext();
        await MigrateTo(db, BeforePurpose);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO "AspNetUsers"
                ("Id", "AccessFailedCount", "EmailConfirmed", "LockoutEnabled", "PhoneNumberConfirmed", "TwoFactorEnabled")
            VALUES ({UserId}, 0, false, false, false, false)
            """);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO "Accounts" ("Id", "UserId", "Name", "Type", "Balance", "CreatedAt")
            VALUES ({AccountId}, {UserId}, 'Brokerage', 'Investment', 1234.50, {CreatedAt})
            """);
    }

    public async Task DisposeAsync()
    {
        await using var db = NewContext();
        await db.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task Up_KeepsExistingAccounts_AndMakesThemSpending()
    {
        await using var db = NewContext();

        await MigrateTo(db);

        var account = await db.Accounts.AsNoTracking().SingleAsync();
        Assert.Equal(AccountId, account.Id);
        Assert.Equal("Brokerage", account.Name);
        // The type isn't used to guess the purpose: the user ticks the box themselves.
        Assert.Equal(AccountType.Investment, account.Type);
        Assert.Equal(1234.50m, account.Balance);
        Assert.Equal("Spending", await StoredPurpose(db));
    }

    [Fact]
    public async Task Down_DropsOnlyThePurpose_AndUpWorksAgain()
    {
        await using var db = NewContext();
        await MigrateTo(db);

        await MigrateTo(db, BeforePurpose);

        var names = await db.Database
            .SqlQuery<string>($"""SELECT "Name" AS "Value" FROM "Accounts" """)
            .ToListAsync();
        Assert.Equal(["Brokerage"], names);

        await MigrateTo(db);
        Assert.Equal("Spending", await StoredPurpose(db));
    }
}
