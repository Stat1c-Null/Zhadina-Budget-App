using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Zhadina.Api.Data;

// IdentityUserContext (not IdentityDbContext) skips the roles tables; there are no roles yet.
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityUserContext<AppUser, Guid>(options);
