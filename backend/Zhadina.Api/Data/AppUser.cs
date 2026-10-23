using Microsoft.AspNetCore.Identity;

namespace Zhadina.Api.Data;

// No extra columns yet; UserName is always set to the email.
public class AppUser : IdentityUser<Guid>;
