using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Client.Models;
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
        var lastSync = _settings.LastSyncedAt ?? DateTimeOffset.MinValue;
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
            catch { }

            var accounts = serverAccounts.Select(DtoMapper.FromDto).ToList();
            var categories = serverCategories.Select(DtoMapper.FromDto).ToList();
            var obligations = serverObligations.Select(DtoMapper.FromDto).ToList();
            var transactions = serverTransactions.Select(DtoMapper.FromDto).ToList();

            var accountMap = accounts.ToDictionary(a => a.Id);
            foreach (var tx in transactions)
            {
                foreach (var entry in tx.Entries)
                {
                    if (!accountMap.TryGetValue(entry.AccountId, out var acc)) continue;
                    if (acc.Type == AccountType.Assets)
                    {
                        var delta = entry.Direction == Models.EntryDirection.Debit ? entry.Amount.Amount : -entry.Amount.Amount;
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

            var accountsToPush = uniqueAccounts.Values.Select(DtoMapper.ToDto).ToList();
            var categories     = _localDb.Categories.Select(DtoMapper.ToDto).ToList();
            var obligations    = _localDb.Obligations.Select(DtoMapper.ToDto).ToList();
            var transactions   = _localDb.Transactions
                .Select(t => DtoMapper.ToDto(t, accountIdMap)).ToList();

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