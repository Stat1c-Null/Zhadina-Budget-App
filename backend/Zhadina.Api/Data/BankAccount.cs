namespace Zhadina.Api.Data;

public class BankAccount
{
    public const int NameMaxLength = 100;

    // Must stay in step with the numeric(14,2) column configured in AppDbContext.
    public const decimal MaxBalance = 999_999_999_999.99m;

    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string Name { get; set; }
    public decimal Balance { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
