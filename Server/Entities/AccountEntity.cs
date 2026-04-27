namespace Server.Entities;

/// <summary>
/// Счёт пользователя на сервере. Kind: 0=Assets, 1=Income, 2=Expense (как на клиенте).
/// AccountType + SecondaryCurrency + ExchangeRate описывают мультивалютность.
/// Soft-delete: при удалении выставляется IsDeleted=true и DeletedAt — данные не теряются,
/// чтобы можно было разворачивать исторические транзакции, ссылающиеся на удалённый счёт.
/// </summary>
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