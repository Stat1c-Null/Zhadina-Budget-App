namespace Zhadina.Api.Data;

// One expense, income or transfer. Saving, editing or deleting one also changes the balances of its accounts
// (see Features/Transactions/BalanceEffects.cs), so the stored balances always include every transaction.
public class Transaction
{
    public const int KindMaxLength = 20;
    public const int CategoryMaxLength = 30;
    public const int DescriptionMaxLength = 200;

    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public TransactionKind Kind { get; set; }

    // For a transfer, the account the money leaves.
    public Guid AccountId { get; set; }

    // Set only on a transfer: the account the money goes to.
    public Guid? ToAccountId { get; set; }

    // Always positive: the kind says which way the money goes.
    public decimal Amount { get; set; }

    // The day it happened, with no time of day, so it reads the same in every time zone.
    public DateOnly Date { get; set; }

    public TransactionCategory? Category { get; set; }
    public string? Description { get; set; }

    // Orders transactions that share a date.
    public DateTimeOffset CreatedAt { get; set; }
}
