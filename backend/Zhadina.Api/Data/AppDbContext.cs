using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Zhadina.Api.Data;

// IdentityUserContext (not IdentityDbContext) skips the roles tables; there are no roles yet.
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityUserContext<AppUser, Guid>(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();

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
    }
}
