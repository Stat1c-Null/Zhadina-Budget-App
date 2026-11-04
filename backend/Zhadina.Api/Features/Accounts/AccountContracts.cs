using Zhadina.Api.Data;

namespace Zhadina.Api.Features.Accounts;

// Nullable so a missing JSON field reaches our validation instead of failing binding opaquely.
// Type is a string for the same reason: an unknown value should be a field error, not a binding failure.
public record AccountRequest(string? Name, string? Type, decimal? Balance);

public record AccountResponse(Guid Id, string Name, AccountType Type, decimal Balance);
