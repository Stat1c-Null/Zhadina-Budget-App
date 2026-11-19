using System.Security.Claims;

namespace Zhadina.Api.Infrastructure;

public static class PrincipalExtensions
{
    // Only call this behind RequireAuthorization: a signed-in principal always carries this claim.
    public static Guid UserId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
