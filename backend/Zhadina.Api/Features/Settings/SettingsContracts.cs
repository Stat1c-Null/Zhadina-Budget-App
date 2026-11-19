namespace Zhadina.Api.Features.Settings;

// PUT replaces every setting, so a null or missing field clears that setting.
public record SettingsRequest(decimal? PriorEarnings);

public record SettingsResponse(decimal? PriorEarnings);
