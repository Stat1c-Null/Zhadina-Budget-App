using System.Text.Json.Serialization;

namespace Zhadina.Api.Data;

// Which way the money goes. An expense leaves the account, income arrives in it,
// and a transfer moves it from AccountId to ToAccountId.
// Stored and sent as the member name, like AccountType, so renaming a member needs a data migration.
[JsonConverter(typeof(JsonStringEnumConverter<TransactionKind>))]
public enum TransactionKind
{
    Expense,
    Income,
    Transfer,
}
