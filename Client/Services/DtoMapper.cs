using System;
using System.Collections.Generic;
using System.Linq;
using Client.Models;
using Shared.Accounts;
using Shared.Categories;
using Shared.Obligations;
using Shared.Transactions;

namespace Client.Services;

/// <summary>
/// Двустороннее преобразование между моделями клиента и Shared DTO.
/// Методы ToDto используются при Push, методы FromDto — при Pull.
/// </summary>
public static class DtoMapper
{
    // ── DTO → Model (Pull) ──────────────────────────────────────────────────

    public static Account FromDto(AccountDto dto) => new()
    {
        Id                    = dto.Id,
        Name                  = dto.Name,
        CurrencyCode          = dto.Currency,
        Type                  = (AccountType)(int)dto.Kind,
        AccountMultiType      = (Models.MultiCurrencyType)(int)dto.AccountType,
        SecondaryCurrencyCode = dto.SecondaryCurrency,
        ExchangeRate          = dto.ExchangeRate,
        Balance               = 0,
        CreatedAt             = DateTimeOffset.Now,
        UpdatedAt             = DateTimeOffset.Now
    };

    public static Category FromDto(CategoryDto dto) => new()
    {
        Id   = dto.Id,
        Name = dto.Name,
        Kind = _incomeCategories.Contains(dto.Name) ? CategoryKind.Income : CategoryKind.Expense
    };

    public static Obligation FromDto(ObligationDto dto) => new()
    {
        Id           = dto.Id,
        Counterparty = dto.Counterparty,
        Amount       = dto.Amount,
        Currency     = dto.Currency,
        Type         = (Models.ObligationType)(int)dto.Type,
        CreatedAt    = dto.CreatedAt,
        DueDate      = dto.DueDate,
        IsPaid       = dto.IsPaid,
        PaidAt       = dto.PaidAt,
        Note         = dto.Note
    };

    public static Transaction FromDto(TransactionDto dto) => new()
    {
        Id          = dto.Id,
        Date        = dto.Date,
        Description = dto.Description ?? string.Empty,
        Entries     = dto.Entries.Select(FromDto).ToList()
    };

    private static Entry FromDto(EntryDto dto) => new()
    {
        Id         = dto.Id,
        AccountId  = dto.AccountId,
        CategoryId = dto.CategoryId,
        Direction  = (Models.EntryDirection)(int)dto.Direction,
        Amount     = new Money(dto.Money.Amount, dto.Money.Currency)
    };

    // ── Model → DTO (Push) ──────────────────────────────────────────────────

    public static AccountDto ToDto(Account a) => new(
        a.Id,
        a.Name,
        (AccountKind)(int)a.Type,
        a.CurrencyCode,
        (Shared.Accounts.MultiCurrencyType)(int)a.AccountMultiType,
        a.SecondaryCurrencyCode,
        a.ExchangeRate);

    public static CategoryDto ToDto(Category c) => new(c.Id, c.Name);

    public static ObligationDto ToDto(Obligation o) => new(
        o.Id, o.Counterparty, o.Amount, o.Currency,
        (Shared.Obligations.ObligationType)(int)o.Type,
        o.CreatedAt, o.DueDate, o.IsPaid, o.PaidAt, o.Note);

    public static TransactionDto ToDto(Transaction t, IReadOnlyDictionary<Guid, Guid>? accountIdMap = null) => new(
        t.Id,
        t.Date,
        t.Description,
        t.Entries.Select(e => ToDto(e, accountIdMap)).ToList());

    private static EntryDto ToDto(Entry e, IReadOnlyDictionary<Guid, Guid>? accountIdMap) => new(
        e.Id,
        accountIdMap != null && accountIdMap.TryGetValue(e.AccountId, out var mapped) ? mapped : e.AccountId,
        e.CategoryId,
        (Shared.Transactions.EntryDirection)(int)e.Direction,
        new Shared.Transactions.MoneyDto(e.Amount.Amount, e.Amount.CurrencyCode));

    // ── Внутреннее ──────────────────────────────────────────────────────────

    private static readonly HashSet<string> _incomeCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Зарплата", "Доход", "Фриланс", "Подработка", "Стипендия", "Пенсия", "Инвестиции"
    };
}
