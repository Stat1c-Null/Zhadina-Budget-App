using Zhadina.Api.Data;

namespace Zhadina.Api.Features.Transactions;

// The one place that decides what a transaction does to account balances. The frontend never works this out;
// it shows the balances the API sends back.
public static class BalanceEffects
{
    // How much money each account gains (positive) or loses (negative).
    public static IEnumerable<(Guid AccountId, decimal Money)> MoneyMoved(
        TransactionKind kind, Guid accountId, Guid? toAccountId, decimal amount) => kind switch
    {
        TransactionKind.Expense => [(accountId, -amount)],
        TransactionKind.Income => [(accountId, amount)],
        TransactionKind.Transfer => [(accountId, -amount), (toAccountId!.Value, amount)],
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    // A credit card's stored balance is the amount owed (ADR-011), so money leaving the card adds to it,
    // and money arriving, such as a payment, takes it down. Must agree with netBalance in frontend/lib/accounts.ts.
    public static decimal StoredChange(AccountType type, decimal money) =>
        type == AccountType.CreditCard ? -money : money;
}
