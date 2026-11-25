using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Zhadina.Api.Data;

// IdentityUserContext (not IdentityDbContext) skips the roles tables; there are no roles yet.
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityUserContext<AppUser, Guid>(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Account>(account =>
        {
            account.Property(a => a.Name).HasMaxLength(Account.NameMaxLength);
            account.Property(a => a.Type).HasConversion<string>().HasMaxLength(Account.TypeMaxLength);
            account.Property(a => a.Balance).HasPrecision(14, 2);
            account.Property(a => a.Purpose).HasConversion<string>().HasMaxLength(Account.PurposeMaxLength);
            account.HasOne<AppUser>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserSettings>(settings =>
        {
            // The user id is the key, so the database itself allows only one settings row per user.
            settings.HasKey(s => s.UserId);
            settings.Property(s => s.PriorEarnings).HasPrecision(14, 2);
            settings.HasOne<AppUser>().WithOne().HasForeignKey<UserSettings>(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Transaction>(transaction =>
        {
            // The API checks all of these first and answers with field errors. These only stop a broken row
            // from reaching the table, because a wrong row would quietly put balances wrong.
            transaction.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Transactions_AmountPositive", """
                    "Amount" > 0
                    """);
                table.HasCheckConstraint("CK_Transactions_ToAccountOnlyOnTransfers", """
                    ("Kind" = 'Transfer') = ("ToAccountId" IS NOT NULL)
                    """);
                table.HasCheckConstraint("CK_Transactions_TransferBetweenTwoAccounts", """
                    "ToAccountId" <> "AccountId"
                    """);
                table.HasCheckConstraint("CK_Transactions_NoCategoryOnTransfers", """
                    "Kind" <> 'Transfer' OR "Category" IS NULL
                    """);
            });
            transaction.Property(t => t.Kind).HasConversion<string>().HasMaxLength(Transaction.KindMaxLength);
            transaction.Property(t => t.Category).HasConversion<string>().HasMaxLength(Transaction.CategoryMaxLength);
            transaction.Property(t => t.Amount).HasPrecision(14, 2);
            transaction.Property(t => t.Description).HasMaxLength(Transaction.DescriptionMaxLength);
            transaction.HasOne<AppUser>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
            // Deleting an account deletes its transactions, transfers to or from it included.
            transaction.HasOne<Account>().WithMany().HasForeignKey(t => t.AccountId).OnDelete(DeleteBehavior.Cascade);
            transaction.HasOne<Account>().WithMany().HasForeignKey(t => t.ToAccountId).OnDelete(DeleteBehavior.Cascade);
            // The history: one user's transactions, newest first.
            transaction.HasIndex(t => new { t.UserId, t.Date, t.CreatedAt }).IsDescending(false, true, true);
        });
    }
}
