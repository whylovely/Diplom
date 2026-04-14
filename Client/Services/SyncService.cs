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

public sealed class SyncService
{
    private readonly ApiService _api;
    private readonly LocalDbService _localDb;
    private readonly SettingsService _settings;

    public SyncService(ApiService api, LocalDbService localDb, SettingsService settings)
    {
        _api = api;
        _localDb = localDb;
        _settings = settings;
    }

    public async Task<SyncResult> SmartSyncAsync()
    {
        var lastSync      = _settings.LastSyncedAt ?? DateTimeOffset.MinValue;
        var lastLocalChange = _localDb.GetLocalLastChangeDate();

        bool hasLocalChanges = lastLocalChange.HasValue && lastLocalChange.Value > lastSync;

        if (hasLocalChanges)
            await PushAllDataToServerAsync();

        var result = await SyncAsync();

        if (result.Success)
            _settings.LastSyncedAt = DateTimeOffset.UtcNow;

        return result;
    }
    public Task<bool> IsServerAvailableAsync() => _api.PingAsync();

    public async Task<int> GetServerTransactionCountAsync()
    {
        try
        {
            var txs = await _api.GetTransactionsAsync() ?? new();
            return txs.Count;
        }
        catch { return -1; }
    }

    public async Task<SyncResult> SyncAsync()
    {
        try
        {
            var serverAccounts = await _api.GetAccountsAsync() ?? new();
            var serverCategories = await _api.GetCategoriesAsync() ?? new();
            var serverObligations = await _api.GetObligationsAsync() ?? new();

            List<TransactionDto> serverTransactions = new();
            try
            {
                serverTransactions = await _api.GetTransactionsAsync() ?? new();
            }
            catch
            {
            }

            var accounts = serverAccounts.Select(MapAccount).ToList();
            var categories = serverCategories.Select(MapCategory).ToList();
            var obligations = serverObligations.Select(MapObligation).ToList();
            var transactions = serverTransactions.Select(MapTransaction).ToList();

            var accountMap = accounts.ToDictionary(a => a.Id);
            foreach (var tx in transactions)
            {
                foreach (var entry in tx.Entries)
                {
                    if (!accountMap.TryGetValue(entry.AccountId, out var acc)) continue;
                    if (acc.Type == AccountType.Assets)
                    {
                        var delta = entry.Direction == Models.EntryDirection.Debit
                            ? entry.Amount.Amount
                            : -entry.Amount.Amount;
                        acc.Balance += delta;
                    }
                }
            }

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

    public async Task<SyncResult> PushAllDataToServerAsync()
    {
        try
        {
            var uniqueAccounts = new Dictionary<string, Account>(StringComparer.OrdinalIgnoreCase);
            var accountIdMap = new Dictionary<Guid, Guid>(); // oldId -> newId

            foreach (var acc in _localDb.Accounts)
            {
                if (uniqueAccounts.TryGetValue(acc.Name, out var existing))
                {
                    accountIdMap[acc.Id] = existing.Id;
                }
                else
                {
                    uniqueAccounts[acc.Name] = acc;
                    accountIdMap[acc.Id] = acc.Id;
                }
            }

            var accountsToPush = uniqueAccounts.Values.Select(a => new AccountDto(
                a.Id, a.Name, (Shared.Accounts.AccountKind)(int)a.Type, a.CurrencyCode,
                (Shared.Accounts.MultiCurrencyType)(int)a.AccountMultiType, a.SecondaryCurrencyCode, a.ExchangeRate)).ToList();
            
            var categories = _localDb.Categories.Select(c => new CategoryDto(
                c.Id, c.Name)).ToList();

            var obligations = _localDb.Obligations.Select(o => new ObligationDto(
                o.Id, o.Counterparty, o.Amount, o.Currency, (Shared.Obligations.ObligationType)(int)o.Type,
                o.CreatedAt, o.DueDate, o.IsPaid, o.PaidAt, o.Note)).ToList();

            var transactions = _localDb.Transactions.Select(t => new TransactionDto(
                t.Id, t.Date, t.Description,
                t.Entries.Select(e => new EntryDto(
                    e.Id, 
                    accountIdMap.TryGetValue(e.AccountId, out var mappedId) ? mappedId : e.AccountId, // Подменяем ID счета, если он был дубликатом
                    e.CategoryId, 
                    (Shared.Transactions.EntryDirection)(int)e.Direction,
                    new Shared.Transactions.MoneyDto(e.Amount.Amount, e.Amount.CurrencyCode))).ToList()
            )).ToList();

            var req = new Shared.Sync.SyncPushRequest(accountsToPush, categories, obligations, transactions);
            await _api.PushAllDataAsync(req);

            return new SyncResult
            {
                Success = true,
                AccountsCount = accountsToPush.Count,
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


    private static Account MapAccount(AccountDto dto) => new()
    {
        Id = dto.Id,
        Name = dto.Name,
        CurrencyCode = dto.Currency,
        Type = (AccountType)(int)dto.Kind,
        AccountMultiType = (Models.MultiCurrencyType)(int)dto.AccountType,
        SecondaryCurrencyCode = dto.SecondaryCurrency,
        ExchangeRate = dto.ExchangeRate,
        Balance = 0,
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