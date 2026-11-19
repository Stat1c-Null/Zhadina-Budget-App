using System.Text.Json.Serialization;

namespace Zhadina.Api.Data;

// What the money in an account is for, which decides the savings and investment totals.
// One value per account, so an account can't be both savings and investment.
// Stored and sent as the member name, like AccountType, so renaming a member needs a data migration.
[JsonConverter(typeof(JsonStringEnumConverter<AccountPurpose>))]
public enum AccountPurpose
{
    Spending,
    Savings,
    Investment,
}
