using System.Text.Json.Serialization;

namespace Zhadina.Api.Data;

// What a transaction was for. Optional, and never set on a transfer.
// Stored and sent as the member name, like AccountType, so adding a member needs no migration but renaming one does.
// Expenses and income each have their own list (see TransactionCategories); Gifts and Other are in both.
[JsonConverter(typeof(JsonStringEnumConverter<TransactionCategory>))]
public enum TransactionCategory
{
    Groceries,
    Dining,
    Housing,
    Utilities,
    Transport,
    Health,
    Shopping,
    Entertainment,
    Subscriptions,
    Travel,
    Education,
    Fees,
    Salary,
    Business,
    Interest,
    InvestmentIncome,
    Refunds,
    Gifts,
    Other,
}

public static class TransactionCategories
{
    // Must match CATEGORIES_BY_KIND in frontend/lib/transactions.ts.
    public static readonly IReadOnlySet<TransactionCategory> Expense = new HashSet<TransactionCategory>
    {
        TransactionCategory.Groceries,
        TransactionCategory.Dining,
        TransactionCategory.Housing,
        TransactionCategory.Utilities,
        TransactionCategory.Transport,
        TransactionCategory.Health,
        TransactionCategory.Shopping,
        TransactionCategory.Entertainment,
        TransactionCategory.Subscriptions,
        TransactionCategory.Travel,
        TransactionCategory.Education,
        TransactionCategory.Gifts,
        TransactionCategory.Fees,
        TransactionCategory.Other,
    };

    public static readonly IReadOnlySet<TransactionCategory> Income = new HashSet<TransactionCategory>
    {
        TransactionCategory.Salary,
        TransactionCategory.Business,
        TransactionCategory.Interest,
        TransactionCategory.InvestmentIncome,
        TransactionCategory.Gifts,
        TransactionCategory.Refunds,
        TransactionCategory.Other,
    };

    // Transfers have no category, so nothing is allowed for them.
    public static bool IsAllowed(TransactionKind kind, TransactionCategory category) => kind switch
    {
        TransactionKind.Expense => Expense.Contains(category),
        TransactionKind.Income => Income.Contains(category),
        _ => false,
    };
}
