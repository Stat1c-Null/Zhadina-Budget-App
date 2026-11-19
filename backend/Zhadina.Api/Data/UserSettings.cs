namespace Zhadina.Api.Data;

// One row per user, created the first time they save their settings. Every setting is nullable,
// so a row can exist before each setting has been chosen and new settings need no backfill.
public class UserSettings
{
    // Must stay in step with the numeric(14,2) column configured in AppDbContext.
    public const decimal MaxPriorEarnings = 999_999_999_999.99m;

    public Guid UserId { get; set; }

    // How much money the user made before they started using the app. Null until they set it.
    public decimal? PriorEarnings { get; set; }
}
