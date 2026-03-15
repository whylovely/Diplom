using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Client.Models;
using Shared.Accounts;
using Shared.Categories;
using Shared.Obligations;
using Shared.Transactions;

namespace Client.Services;

/// <summary>
/// Сервис синхронизации: pull данных с сервера → замена локальной SQLite.
/// Стратегия: сервер главный.
/// </summary>
public sealed class SyncService
{
    private readonly ApiService _api;
    private readonly LocalDbService _localDb;

    public SyncService(ApiService api, LocalDbService localDb)
    {
        _api = api;
        _localDb = localDb;
    }

    /// <summary>
    /// Проверить доступность сервера.
    /// </summary>
    public Task<bool> IsServerAvailableAsync() => _api.PingAsync();

    /// <summary>
    /// Получить количество транзакций на сервере (для сравнения с локальными).
    /// </summary>
    public async Task<int> GetServerTransactionCountAsync()
    {
        try
        {
            var txs = await _api.GetTransactionsAsync() ?? new();
            return txs.Count;
        }
        catch { return -1; }
    }

    /// <summary>
    /// Полная синхронизация: загрузить данные с сервера и записать в локальную БД.
    /// </summary>
    public async Task<SyncResult> SyncAsync()
    {
        try
        {
            // 1. Скачать основные данные
            var serverAccounts = await _api.GetAccountsAsync() ?? new();
            var serverCategories = await _api.GetCategoriesAsync() ?? new();
            var serverObligations = await _api.GetObligationsAsync() ?? new();

            // 2. Попробовать скачать транзакции (endpoint может не существовать)
            List<TransactionDto> serverTransactions = new();
            try
            {
                serverTransactions = await _api.GetTransactionsAsync() ?? new();
            }
            catch
            {
                // Если endpoint отсутствует — продолжаем без транзакций
            }

            // 3. Маппинг DTO → клиентские модели
            var accounts = serverAccounts.Select(MapAccount).ToList();
            var categories = serverCategories.Select(MapCategory).ToList();
            var obligations = serverObligations.Select(MapObligation).ToList();
            var transactions = serverTransactions.Select(MapTransaction).ToList();

            // 4. Создать технические счета для каждой категории
            //    Клиент использует «Расходы: X» и «Доходы: X» для двойной записи
            foreach (var cat in categories)
            {
                var expAcc = new Account
                {
                    Name = $"Расходы: {cat.Name}",
                    CurrencyCode = "RUB",
                    Type = AccountType.Expense,
                    CreatedAt = DateTimeOffset.Now,
                    UpdatedAt = DateTimeOffset.Now
                };
                accounts.Add(expAcc);

                var incAcc = new Account
                {
                    Name = $"Доходы: {cat.Name}",
                    CurrencyCode = "RUB",
                    Type = AccountType.Income,
                    CreatedAt = DateTimeOffset.Now,
                    UpdatedAt = DateTimeOffset.Now
                };
                accounts.Add(incAcc);
            }

            // 5. Пересчитать балансы из транзакций
            var accountMap = accounts.ToDictionary(a => a.Id);
            foreach (var tx in transactions)
            {
                foreach (var entry in tx.Entries)
                {
                    if (!accountMap.TryGetValue(entry.AccountId, out var acc)) continue;
                    if (acc.Type == AccountType.Assets)
                    {
                        // Debit (0) → +, Credit (1) → -, любое другое → -
                        var delta = entry.Direction == Models.EntryDirection.Debit
                            ? entry.Amount.Amount
                            : -entry.Amount.Amount;
                        acc.Balance += delta;
                    }
                }
            }

            // 6. Заменить локальные данные
            _localDb.ReplaceAllData(accounts, categories, obligations, transactions);

            return new SyncResult
            {
                Success = true,
                AccountsCount = serverAccounts.Count,
                CategoriesCount = categories.Count,
                ObligationsCount = obligations.Count,
                TransactionsCount = transactions.Count
            };
        }
        catch (Exception ex)
        {
            return new SyncResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Полная синхронизация: отправить локальные данные на сервер с перезаписью.
    /// </summary>
    public async Task<SyncResult> PushAllDataToServerAsync()
    {
        try
        {
            var accounts = _localDb.Accounts.Select(a => new AccountDto(
                a.Id, a.Name, (Shared.Accounts.AccountKind)a.Type, a.CurrencyCode,
                (Shared.Accounts.MultiCurrencyType)a.AccountMultiType, a.SecondaryCurrencyCode, a.ExchangeRate)).ToList();
            
            var categories = _localDb.Categories.Select(c => new CategoryDto(
                c.Id, c.Name)).ToList();

            var obligations = _localDb.Obligations.Select(o => new ObligationDto(
                o.Id, o.Counterparty, o.Amount, o.Currency, (Shared.Obligations.ObligationType)o.Type,
                o.CreatedAt, o.DueDate, o.IsPaid, o.PaidAt, o.Note)).ToList();

            var transactions = _localDb.Transactions.Select(t => new TransactionDto(
                t.Id, t.Date, t.Description,
                t.Entries.Select(e => new EntryDto(
                    e.Id, e.AccountId, e.CategoryId, (Shared.Transactions.EntryDirection)e.Direction,
                    new Shared.Transactions.MoneyDto(e.Amount.Amount, e.Amount.CurrencyCode))).ToList()
            )).ToList();

            var req = new Shared.Sync.SyncPushRequest(accounts, categories, obligations, transactions);
            await _api.PushAllDataAsync(req);

            return new SyncResult
            {
                Success = true,
                AccountsCount = accounts.Count,
                CategoriesCount = categories.Count,
                ObligationsCount = obligations.Count,
                TransactionsCount = transactions.Count
            };
        }
        catch (Exception ex)
        {
            return new SyncResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    // ─── Маппинг DTO → Client Models ─────────────────────

    private static Account MapAccount(AccountDto dto) => new()
    {
        Id = dto.Id,
        Name = dto.Name,
        CurrencyCode = dto.Currency,
        Type = (AccountType)(int)dto.Kind,
        AccountMultiType = (Models.MultiCurrencyType)(int)dto.AccountType,
        SecondaryCurrencyCode = dto.SecondaryCurrency,
        ExchangeRate = dto.ExchangeRate,
        Balance = 0, // будет пересчитан из транзакций
        CreatedAt = DateTimeOffset.Now,
        UpdatedAt = DateTimeOffset.Now
    };

    private static readonly HashSet<string> IncomeCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Зарплата", "Доход", "Фриланс", "Подработка", "Стипендия", "Пенсия", "Инвестиции"
    };

    private static Category MapCategory(CategoryDto dto) => new()
    {
        Id = dto.Id,
        Name = dto.Name,
        Kind = IncomeCategories.Contains(dto.Name) ? CategoryKind.Income : CategoryKind.Expense
    };

    private static Obligation MapObligation(ObligationDto dto) => new()
    {
        Id = dto.Id,
        Counterparty = dto.Counterparty,
        Amount = dto.Amount,
        Currency = dto.Currency,
        Type = (Models.ObligationType)(int)dto.Type,
        CreatedAt = dto.CreatedAt,
        DueDate = dto.DueDate,
        IsPaid = dto.IsPaid,
        PaidAt = dto.PaidAt,
        Note = dto.Note
    };

    private static Transaction MapTransaction(TransactionDto dto) => new()
    {
        Id = dto.Id,
        Date = dto.Date,
        Description = dto.Description ?? string.Empty,
        Entries = dto.Entries.Select(e => new Entry
        {
            Id = e.Id,
            AccountId = e.AccountId,
            CategoryId = e.CategoryId,
            Direction = (Models.EntryDirection)(int)e.Direction,
            Amount = new Money(e.Money.Amount, e.Money.Currency)
        }).ToList()
    };
}

public sealed class SyncResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int AccountsCount { get; init; }
    public int CategoriesCount { get; init; }
    public int ObligationsCount { get; init; }
    public int TransactionsCount { get; init; }
}