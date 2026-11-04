using System.Text.Json.Serialization;

namespace Zhadina.Api.Data;

// Stored and sent as the member name, so renaming a member needs a data migration and a frontend change.
[JsonConverter(typeof(JsonStringEnumConverter<AccountType>))]
public enum AccountType
{
    Bank,
    Cash,
    Investment,
    RothIra,
    TraditionalIra,
    Retirement401k,
    CreditCard,
    Other,
}
