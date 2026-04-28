// DTO счетов для синхронизации клиент↔сервер. Клиентская модель Account мапится в DtoMapper.
namespace Shared.Accounts;

// Тип счёта
public enum AccountKind
{
    Assets = 0,
    Income = 1,
    Expenses = 2
}

public enum MultiCurrencyType
{
    Standard = 0,
    MultiCurrency = 1
}

public sealed record AccountDto(
    Guid Id,
    string Name,
    AccountKind Kind,
    string Currency,
    MultiCurrencyType AccountType,
    string? SecondaryCurrency,
    decimal? ExchangeRate);

public sealed record CreateAccountRequest(
    string Name,
    AccountKind Kind,
    string Currency,
    MultiCurrencyType AccountType = MultiCurrencyType.Standard,
    string? SecondaryCurrency = null,
    decimal? ExchangeRate = null);

public sealed record UpdateAccountRequest(
    string Name,
    AccountKind Kind,
    string Currency,
    MultiCurrencyType AccountType = MultiCurrencyType.Standard,
    string? SecondaryCurrency = null,
    decimal? ExchangeRate = null);