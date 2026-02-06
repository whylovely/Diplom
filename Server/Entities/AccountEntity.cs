using System.ComponentModel.DataAnnotations;

namespace Server.Entities;

public enum AccountType
{
    Assets = 1,
    Income = 2,
    Expenses = 3,
    Liability = 4,
}

public sealed class AccountEntity
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    [Required] public string Name { get; set; } = "";
    [Required] public string CurrencyCode { get; set; } = "RUB";

    public AccountType Type { get; set; } = AccountType.Assets;

    public decimal InitialBalance { get; set; }
}