namespace Zhadina.Api.Features.BankAccounts;

// Nullable so a missing JSON field reaches our validation instead of failing binding opaquely.
public record BankAccountRequest(string? Name, decimal? Balance);

public record BankAccountResponse(Guid Id, string Name, decimal Balance);
