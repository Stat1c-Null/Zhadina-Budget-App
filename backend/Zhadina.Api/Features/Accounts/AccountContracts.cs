using Zhadina.Api.Data;

namespace Zhadina.Api.Features.Accounts;

// Nullable so a missing JSON field reaches our validation instead of failing binding opaquely.
// Type and Purpose are strings for the same reason: an unknown value should be a field error, not a binding failure.
// Purpose may be left out or null, which means Spending.
public record AccountRequest(string? Name, string? Type, decimal? Balance, string? Purpose);

public record AccountResponse(Guid Id, string Name, AccountType Type, decimal Balance, AccountPurpose Purpose);
