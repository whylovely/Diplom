namespace Server.Entities;

public sealed class AccountEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public UserEntity User { get; set; } = default!;

    public string Name { get; set; } = default!;
    public int Kind { get; set; }          
    public string Currency { get; set; } = "RUB";

    public int AccountType { get; set; }           // 0 = Standard, 1 = MultiCurrency
    public string? SecondaryCurrency { get; set; }  // ISO-код второй валюты
    public decimal? ExchangeRate { get; set; }      // курс конвертации

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}