using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;
using Zhadina.Api.Data;

namespace Zhadina.Api.Tests;

// The rename is hand-written because EF scaffolds it as a drop and create, so these guard real user data.
public class GeneralizeAccountsMigrationTests : IAsyncLifetime
{
    private const string BeforeRename = "20261103204621_AddBankAccounts";

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly DateTimeOffset CreatedAt = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    private readonly string _connectionString =
        $"{Environment.GetEnvironmentVariable("ZHADINA_TEST_PG") ?? "Host=localhost;Port=5432;Username=postgres"};" +
        $"Database=zhadina_test_{Guid.NewGuid():N}";

    private AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_connectionString).Options);

    private static Task MigrateTo(AppDbContext db, string? migration = null) =>
        db.GetService<IMigrator>().MigrateAsync(migration);

    // A database as it looks today: the old table, holding one user's account.
    public async Task InitializeAsync()
    {
        await using var db = NewContext();
        await MigrateTo(db, BeforeRename);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO "AspNetUsers"
                ("Id", "AccessFailedCount", "EmailConfirmed", "LockoutEnabled", "PhoneNumberConfirmed", "TwoFactorEnabled")
            VALUES ({UserId}, 0, false, false, false, false)
            """);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO "BankAccounts" ("Id", "UserId", "Name", "Balance", "CreatedAt")
            VALUES ({AccountId}, {UserId}, 'Checking', 1234.50, {CreatedAt})
            """);
    }

    public async Task DisposeAsync()
    {
        await using var db = NewContext();
        await db.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task Up_KeepsExistingAccounts_AndMarksThemAsBank()
    {
        await using var db = NewContext();

        await MigrateTo(db);

        var account = await db.Accounts.AsNoTracking().SingleAsync();
        Assert.Equal(AccountId, account.Id);
        Assert.Equal(UserId, account.UserId);
        Assert.Equal("Checking", account.Name);
        Assert.Equal(AccountType.Bank, account.Type);
        Assert.Equal(1234.50m, account.Balance);
        Assert.Equal(CreatedAt, account.CreatedAt);
    }

    [Fact]
    public async Task Up_LeavesNoDefaultType_SoEveryNewRowMustChooseOne()
    {
        await using var db = NewContext();
        await MigrateTo(db);

        var insert = () => db.Database.ExecuteSqlAsync($"""
            INSERT INTO "Accounts" ("Id", "UserId", "Name", "Balance", "CreatedAt")
            VALUES ({Guid.NewGuid()}, {UserId}, 'No type', 1, {CreatedAt})
            """);

        await Assert.ThrowsAsync<Npgsql.PostgresException>(insert);
    }

    [Fact]
    public async Task Up_KeepsTheCascadeFromUsers()
    {
        await using var db = NewContext();
        await MigrateTo(db);

        await db.Database.ExecuteSqlAsync($"""DELETE FROM "AspNetUsers" WHERE "Id" = {UserId}""");

        Assert.Empty(await db.Accounts.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Down_RestoresTheOldTable_AndUpWorksAgain()
    {
        await using var db = NewContext();
        await MigrateTo(db);

        await MigrateTo(db, BeforeRename);

        var names = await db.Database
            .SqlQuery<string>($"""SELECT "Name" AS "Value" FROM "BankAccounts" """)
            .ToListAsync();
        Assert.Equal(["Checking"], names);

        await MigrateTo(db);
        Assert.Equal(AccountId, (await db.Accounts.AsNoTracking().SingleAsync()).Id);
    }
}
