using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Zhadina.Api.Data;

// IdentityUserContext (not IdentityDbContext) skips the roles tables; there are no roles yet.
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityUserContext<AppUser, Guid>(options)
{
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<BankAccount>(account =>
        {
            account.Property(a => a.Name).HasMaxLength(BankAccount.NameMaxLength);
            account.Property(a => a.Balance).HasPrecision(14, 2);
            account.HasOne<AppUser>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
