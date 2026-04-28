using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Client.Data;
using Client.Models;
using Microsoft.Data.Sqlite;

namespace Client.Repositories;

// Хранилище счетов и групп счетов
public sealed class AccountRepository
{
    private readonly SqliteConFactory _factory;
    public event Action? Changed;
    private void RaiseChanged() => Changed?.Invoke();

    private readonly List<Account> _accounts = new();
    private readonly List<AccountGroup> _accountGroups = new();

    private readonly Dictionary<Guid, Guid> _expenseAccountByCategoryId = new();
    private readonly Dictionary<Guid, Guid> _incomeAccountByCategoryId  = new();

    public IReadOnlyList<Account> All => _accounts;
    public IReadOnlyList<AccountGroup> Groups => _accountGroups;

    public AccountRepository(SqliteConFactory f) => _factory = f;

    public void Load()
    {
        _accounts.Clear();
        _expenseAccountByCategoryId.Clear();
        _incomeAccountByCategoryId.Clear();
        _accountGroups.Clear();

        using var conn = _factory.Open();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT * FROM Accounts WHERE IsDeleted = 0";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var acc = new Account
                {
                    Id = Guid.Parse(r.GetString(r.GetOrdinal("Id"))),
                    Name = r.GetString(r.GetOrdinal("Name")),
                    GroupId = r.IsDBNull(r.GetOrdinal("GroupId")) ? null : Guid.Parse(r.GetString(r.GetOrdinal("GroupId"))),
                    CurrencyCode = r.GetString(r.GetOrdinal("CurrencyCode")),
                    InitialBalance = (decimal)r.GetDouble(r.GetOrdinal("InitialBalance")),
                    Balance = (decimal)r.GetDouble(r.GetOrdinal("Balance")),
                    Type = (AccountType)r.GetInt32(r.GetOrdinal("Type")),
                    AccountMultiType = (MultiCurrencyType)r.GetInt32(r.GetOrdinal("AccountMultiType")),
                    SecondaryCurrencyCode = r.IsDBNull(r.GetOrdinal("SecondaryCurrencyCode")) ? null : r.GetString(r.GetOrdinal("SecondaryCurrencyCode")),
                    ExchangeRate = r.IsDBNull(r.GetOrdinal("ExchangeRate")) ? null : (decimal?)r.GetDouble(r.GetOrdinal("ExchangeRate")),
                    SecondaryBalance = (decimal)r.GetDouble(r.GetOrdinal("SecondaryBalance")),
                    IsDeleted = r.GetInt32(r.GetOrdinal("IsDeleted")) == 1,
                    CreatedAt = DateTimeOffset.Parse(r.GetString(r.GetOrdinal("CreatedAt"))),
                    UpdatedAt = DateTimeOffset.Parse(r.GetString(r.GetOrdinal("UpdatedAt")))
                };
                _accounts.Add(acc);
            }
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT * FROM AccountGroups ORDER BY SortOrder, Name";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                _accountGroups.Add(new AccountGroup
                {
                    Id = Guid.Parse(r.GetString(r.GetOrdinal("Id"))),
                    Name = r.GetString(r.GetOrdinal("Name")),
                    SortOrder = r.GetInt32(r.GetOrdinal("SortOrder"))
                });
            }
        }
    }

    // Восстанавливает словари связей «категория → технический счёт» 
    public void RebuildTechnicalAccountMappings(IReadOnlyList<Category> categories)
    {
        _expenseAccountByCategoryId.Clear();
        _incomeAccountByCategoryId.Clear();

        foreach (var cat in categories)
        {
            var expAcc = _accounts.FirstOrDefault(a => a.Type == AccountType.Expense && a.Name == $"Расходы: {cat.Name}");
            if (expAcc != null) _expenseAccountByCategoryId[cat.Id] = expAcc.Id;

            var incAcc = _accounts.FirstOrDefault(a => a.Type == AccountType.Income && a.Name == $"Доходы: {cat.Name}");
            if (incAcc != null) _incomeAccountByCategoryId[cat.Id] = incAcc.Id;
        }
    }

    public void Add(Account account)
    {
        using var conn = _factory.Open();

        SqliteConFactory.Exec(conn, @"INSERT INTO Accounts
            (Id, Name, GroupId, CurrencyCode, InitialBalance, Balance, Type, AccountMultiType,
             SecondaryCurrencyCode, ExchangeRate, SecondaryBalance, IsDeleted, CreatedAt, UpdatedAt)
            VALUES (@Id, @Name, @Group, @Cur, @Init, @Bal, @Type, @Multi, @Sec, @Rate, @SecBal, @IsDeleted, @Created, @Updated)",
            ("@Id", account.Id.ToString()),
            ("@Name", account.Name),
            ("@Group", (object?)account.GroupId?.ToString() ?? DBNull.Value),
            ("@Cur", account.CurrencyCode),
            ("@Init", (double)account.InitialBalance),
            ("@Bal", (double)account.Balance),
            ("@Type", (int)account.Type),
            ("@Multi", (int)account.AccountMultiType),
            ("@Sec", (object?)account.SecondaryCurrencyCode ?? DBNull.Value),
            ("@Rate", account.ExchangeRate.HasValue ? (object)(double)account.ExchangeRate.Value : DBNull.Value),
            ("@SecBal", (double)account.SecondaryBalance),
            ("@IsDeleted", account.IsDeleted ? 1 : 0),
            ("@Created", account.CreatedAt.ToString("O")),
            ("@Updated", account.UpdatedAt.ToString("O")));

        _accounts.Add(account);
        RaiseChanged();
    }

    public void Rename(Guid id, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return;
        var acc = _accounts.FirstOrDefault(a => a.Id == id);
        if (acc is null) return;

        acc.Name = newName.Trim();
        acc.UpdatedAt = DateTimeOffset.Now;

        using var conn = _factory.Open();
        SqliteConFactory.Exec(conn, "UPDATE Accounts SET Name = @Name, UpdatedAt = @Updated WHERE Id = @Id",
            ("@Name", acc.Name), ("@Updated", acc.UpdatedAt.ToString("O")), ("@Id", acc.Id.ToString()));

        RaiseChanged();
    }

    public void Remove(Guid id)
    {
        var acc = _accounts.FirstOrDefault(a => a.Id == id);
        if (acc is null) return;

        acc.IsDeleted = true;
        acc.UpdatedAt = DateTimeOffset.Now;

        using var conn = _factory.Open();
        SqliteConFactory.Exec(conn, "UPDATE Accounts SET IsDeleted = 1, UpdatedAt = @Updated WHERE Id = @Id",
            ("@Updated", acc.UpdatedAt.ToString("O")), ("@Id", id.ToString()));

        RaiseChanged();
    }

    // При смене базовой валюты пользователем переключает Asset-счета
    public void UpdateBaseCurrency(string newBaseCurrency)
    {
        using var conn = _factory.Open();
        using var tran = conn.BeginTransaction();
        bool changed = false;

        foreach (var acc in _accounts)
        {
            if (acc.Type != AccountType.Assets) continue;

            if (acc.CurrencyCode != newBaseCurrency)
            {
                if (!acc.IsMultiCurrency || acc.SecondaryCurrencyCode != newBaseCurrency)
                {
                    acc.IsMultiCurrency = true;
                    acc.SecondaryCurrencyCode = newBaseCurrency;
                    changed = true;

                    SqliteConFactory.Exec(conn, "UPDATE Accounts SET AccountMultiType = @Multi, SecondaryCurrencyCode = @Sec, UpdatedAt = @Updated WHERE Id = @Id",
                        ("@Multi", (int)acc.AccountMultiType),
                        ("@Sec", acc.SecondaryCurrencyCode),
                        ("@Updated", DateTimeOffset.Now.ToString("O")),
                        ("@Id", acc.Id.ToString()));
                }
            }
            else
            {
                if (acc.IsMultiCurrency)
                {
                    acc.IsMultiCurrency = false;
                    acc.SecondaryCurrencyCode = null;
                    changed = true;

                    SqliteConFactory.Exec(conn, "UPDATE Accounts SET AccountMultiType = @Multi, SecondaryCurrencyCode = NULL, UpdatedAt = @Updated WHERE Id = @Id",
                        ("@Multi", (int)acc.AccountMultiType),
                        ("@Updated", DateTimeOffset.Now.ToString("O")),
                        ("@Id",  acc.Id.ToString()));
                }
            }
        }

        tran.Commit();
        if (changed) RaiseChanged();
    }

    public void SetGroup(Guid accountId, Guid? groupId)
    {
        var acc = _accounts.FirstOrDefault(a => a.Id == accountId);
        if (acc is null) return;

        acc.GroupId   = groupId;
        acc.UpdatedAt = DateTimeOffset.Now;

        using var conn = _factory.Open();
        SqliteConFactory.Exec(conn, "UPDATE Accounts SET GroupId = @Group, UpdatedAt = @Updated WHERE Id = @Id",
            ("@Group",   (object?)groupId?.ToString() ?? DBNull.Value),
            ("@Updated", acc.UpdatedAt.ToString("O")),
            ("@Id",      accountId.ToString()));

        RaiseChanged();
    }

    public Account GetExpenseAccountForCategory(Guid categoryId)
    {
        if (!_expenseAccountByCategoryId.TryGetValue(categoryId, out var accId))
            throw new InvalidOperationException($"Expense account not found for category {categoryId}");
        return _accounts.Single(a => a.Id == accId);
    }

    public Account GetIncomeAccountForCategory(Guid categoryId)
    {
        if (!_incomeAccountByCategoryId.TryGetValue(categoryId, out var accId))
            throw new InvalidOperationException($"Income account not found for category {categoryId}");
        return _accounts.Single(a => a.Id == accId);
    }

    public DateTimeOffset? GetLastChangeDate()
    {
        if (_accounts.Count == 0) return null;
        return _accounts.Max(a => a.UpdatedAt);
    }

    // Создаёт пару технических счетов («Расходы: X» / «Доходы: X»)
    public void CreateTechnicalAccountsForCategory(Category category)
    {
        var now = DateTimeOffset.Now;

        var expense = new Account
        {
            Id = Guid.NewGuid(),
            Name = $"Расходы: {category.Name}",
            CurrencyCode = "RUB",
            Type = AccountType.Expense,
            CreatedAt = now,
            UpdatedAt = now
        };

        var income = new Account
        {
            Id = Guid.NewGuid(),
            Name = $"Доходы: {category.Name}",
            CurrencyCode = "RUB",
            Type = AccountType.Income,
            CreatedAt = now,
            UpdatedAt = now
        };

        Add(expense);
        Add(income);

        _expenseAccountByCategoryId[category.Id] = expense.Id;
        _incomeAccountByCategoryId[category.Id] = income.Id;
    }

    public Task AddGroupAsync(AccountGroup group)
    {
        using var conn = _factory.Open();
        SqliteConFactory.Exec(conn, "INSERT INTO AccountGroups (Id, Name, SortOrder) VALUES (@Id, @Name, @Sort)",
            ("@Id", group.Id.ToString()), ("@Name", group.Name), ("@Sort", group.SortOrder));

        _accountGroups.Add(group);
        RaiseChanged();
        return Task.CompletedTask;
    }

    public Task UpdateGroupAsync(AccountGroup group)
    {
        using var conn = _factory.Open();
        SqliteConFactory.Exec(conn, "UPDATE AccountGroups SET Name = @Name, SortOrder = @Sort WHERE Id = @Id",
            ("@Name", group.Name), ("@Sort", group.SortOrder), ("@Id", group.Id.ToString()));

        var idx = _accountGroups.FindIndex(g => g.Id == group.Id);
        if (idx >= 0) _accountGroups[idx] = group;
        RaiseChanged();
        return Task.CompletedTask;
    }

    public Task DeleteGroupAsync(Guid id)
    {
        using var conn = _factory.Open();
        SqliteConFactory.Exec(conn, "DELETE FROM AccountGroups WHERE Id = @Id", ("@Id", id.ToString()));
        SqliteConFactory.Exec(conn, "UPDATE Accounts SET GroupId = NULL WHERE GroupId = @Id", ("@Id", id.ToString()));

        _accountGroups.RemoveAll(g => g.Id == id);
        foreach (var a in _accounts.Where(x => x.GroupId == id))
            a.GroupId = null;

        RaiseChanged();
        return Task.CompletedTask;
    }
}