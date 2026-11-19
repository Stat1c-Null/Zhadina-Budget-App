using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Zhadina.Api.Data;
using Zhadina.Api.Infrastructure;

namespace Zhadina.Api.Features.Settings;

public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings").RequireAuthorization();

        group.MapGet("/", Get);
        group.MapPut("/", Update);

        return app;
    }

    // A user who has never saved settings has no row yet, which reads as every setting unset.
    private static async Task<Ok<SettingsResponse>> Get(ClaimsPrincipal principal, AppDbContext db)
    {
        var userId = principal.UserId();
        var settings = await db.UserSettings.SingleOrDefaultAsync(s => s.UserId == userId);
        return TypedResults.Ok(new SettingsResponse(settings?.PriorEarnings));
    }

    private static async Task<Results<Ok<SettingsResponse>, ValidationProblem>> Update(
        SettingsRequest request, ClaimsPrincipal principal, AppDbContext db)
    {
        if (Validate(request) is { } errors)
            return TypedResults.ValidationProblem(errors);

        var userId = principal.UserId();
        var settings = await db.UserSettings.SingleOrDefaultAsync(s => s.UserId == userId);
        if (settings is null)
        {
            settings = new UserSettings { UserId = userId };
            db.UserSettings.Add(settings);
        }

        settings.PriorEarnings = request.PriorEarnings;
        await db.SaveChangesAsync();

        return TypedResults.Ok(new SettingsResponse(settings.PriorEarnings));
    }

    private static Dictionary<string, string[]>? Validate(SettingsRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.PriorEarnings is { } earnings)
        {
            if (earnings < 0)
                errors["priorEarnings"] = ["Earnings can't be negative."];
            else if (earnings > UserSettings.MaxPriorEarnings)
                errors["priorEarnings"] = ["Earnings are too large."];
            else if (decimal.Round(earnings, 2) != earnings)
                errors["priorEarnings"] = ["Earnings can't have more than 2 decimal places."];
        }

        return errors.Count > 0 ? errors : null;
    }
}
