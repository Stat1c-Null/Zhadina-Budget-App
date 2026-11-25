using Zhadina.Api.Data;
using Zhadina.Api.Features.Accounts;

namespace Zhadina.Api.Features.Transactions;

// Every field is nullable, and ids, kind, category and date are strings, so a missing or malformed value
// reaches our validation and comes back as a field error instead of failing JSON binding with a bare 400.
// AccountId is the account the money leaves on a transfer. Date is "YYYY-MM-DD".
public record TransactionRequest(
    string? Kind,
    string? AccountId,
    string? ToAccountId,
    decimal? Amount,
    string? Date,
    string? Category,
    string? Description);

public record TransactionResponse(
    Guid Id,
    TransactionKind Kind,
    Guid AccountId,
    Guid? ToAccountId,
    decimal Amount,
    DateOnly Date,
    TransactionCategory? Category,
    string? Description);

// Writes answer with the accounts the transaction touches, at their new balances,
// so the page can update its account list without asking again.
public record TransactionChangeResponse(TransactionResponse Transaction, List<AccountResponse> Accounts);

public record AccountsChangeResponse(List<AccountResponse> Accounts);
