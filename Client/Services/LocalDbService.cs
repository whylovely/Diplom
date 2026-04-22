using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Client.Models;
using Microsoft.Data.Sqlite;
using Client.Data;
using Client.Repositories;

namespace Client.Services;

public sealed class LocalDbService : IDataService   // Основа - SQLite
{
    private readonly Client.Data.SqliteConFactory _factory;
    public event Action? DataChanged;
    private void RaiseChanged() => DataChanged?.Invoke();

    private readonly AccountRepository _accountsRepo;
    public IReadOnlyList<Account> Accounts => _accountsRepo.All;
    public IReadOnlyList<AccountGroup> AccountGroups => _accountsRepo.Groups;

    private readonly CategoriesRepository _categoriesRepo;
    public IReadOnlyList<Category> Categories => _categoriesRepo.All;

    private readonly TransactionsRepository _transactionsRepo;
    public IReadOnlyList<Transaction> Transactions => _transactionsRepo.All;

    private readonly ObligationRepository _obligationsRepo;
    public IReadOnlyList<Obligation> Obligations => _obligationsRepo.All;

    private readonly CurrencyRatesRepository _currencyRatesRepo;
    public IReadOnlyList<CurrencyRate> CurrencyRates => _currencyRatesRepo.All;

    private readonly TemplatesRepository _templatesRepo;
    public IReadOnlyList<TransactionTemplate> Templates => _templatesRepo.All;

    public LocalDbService()
    {
        _factory = new Client.Data.SqliteConFactory();

        new DbInitializer(_factory).Initialize();
        new DbSeeder(_factory).SeedIfEmpty();

        _accountsRepo      = new AccountRepository(_factory);
        _categoriesRepo    = new CategoriesRepository(_factory);
        _transactionsRepo  = new TransactionsRepository(_factory);
        _obligationsRepo   = new ObligationRepository(_factory);
        _templatesRepo     = new TemplatesRepository(_factory);
        _currencyRatesRepo = new CurrencyRatesRepository(_factory);

        _categoriesRepo.Load();
        _accountsRepo.Load();
        _accountsRepo.RebuildTechnicalAccountMappings(_categoriesRepo.All);
        _transactionsRepo.Load();
        _obligationsRepo.Load();
        _templatesRepo.Load();
        _currencyRatesRepo.Load();

        _accountsRepo.Changed      += RaiseChanged;
        _categoriesRepo.Changed    += RaiseChanged;
        _transactionsRepo.Changed  += RaiseChanged;
        _obligationsRepo.Changed   += RaiseChanged;
        _templatesRepo.Changed     += RaiseChanged;
        _currencyRatesRepo.Changed += RaiseChanged;
    }

    public DateTimeOffset? GetLocalLastChangeDate()
    {
        var dates = new List<DateTimeOffset>();
        if (_transactionsRepo.All.Count > 0) dates.Add(_transactionsRepo.All.Max(t => t.Date));
        if (_obligationsRepo.All.Count > 0) dates.Add(_obligationsRepo.All.Max(o => o.CreatedAt));
        return dates.Count > 0 ? dates.Max() : null;
    }

    public void AddAccount(Account account) => _accountsRepo.Add(account);
    public void RenameAccount(Guid id, string newName) => _accountsRepo.Rename(id, newName);
    public void RemoveAccount(Guid id) => _accountsRepo.Remove(id);
    public void UpdateAccountsBaseCurrency(string newCurrency) => _accountsRepo.UpdateBaseCurrency(newCurrency);
    public void SetAccountGroup(Guid accountId, Guid? groupId) => _accountsRepo.SetGroup(accountId, groupId);
    public bool IsAccountUsed(Guid id) => _transactionsRepo.All.Any(tx => tx.Entries.Any(e => e.AccountId == id));
    public Account GetExpenseAccountForCategory(Guid categoryId) => _accountsRepo.GetExpenseAccountForCategory(categoryId);
    public Account GetIncomeAccountForCategory(Guid categoryId) => _accountsRepo.GetIncomeAccountForCategory(categoryId);
    public Task AddAccountGroupAsync(AccountGroup group) => _accountsRepo.AddGroupAsync(group);
    public Task UpdateAccountGroupAsync(AccountGroup group) => _accountsRepo.UpdateGroupAsync(group);
    public Task DeleteAccountGroupAsync(Guid id) => _accountsRepo.DeleteGroupAsync(id);

    public void AddCategory(Category category)
    {
        _categoriesRepo.Add(category);
        _accountsRepo.CreateTechnicalAccountsForCategory(category);
    }

    public void RemoveCategory(Category category) => _categoriesRepo.Remove(category);

    public Task PostTransactionAsync(Transaction tx)
    {
        if (tx.Entries.Count < 2)
            throw new InvalidOperationException("Транзакция не содержит двух проводок");

        using var conn = _factory.Open();
        using var tran = conn.BeginTransaction();

        SqliteConFactory.Exec(conn, @"INSERT INTO Transactions (Id, Date, Description, CreatedAt)
                     VALUES (@Id, @Date, @Desc, @Created)",
            ("@Id", tx.Id.ToString()),
            ("@Date", tx.Date.ToString("O")),
            ("@Desc", tx.Description),
            ("@Created", DateTimeOffset.Now.ToString("O")));

        foreach (var e in tx.Entries)
        {
            SqliteConFactory.Exec(conn, @"INSERT INTO Entries (Id, TransactionId, AccountId, CategoryId, Direction, Amount, CurrencyCode)
                         VALUES (@Id, @TxId, @AccId, @CatId, @Dir, @Amt, @Cur)",
                ("@Id", e.Id.ToString()),
                ("@TxId", tx.Id.ToString()),
                ("@AccId", e.AccountId.ToString()),
                ("@CatId", e.CategoryId.HasValue ? (object)e.CategoryId.Value.ToString() : DBNull.Value),
                ("@Dir", (int)e.Direction),
                ("@Amt", (double)e.Amount.Amount),
                ("@Cur", e.Amount.CurrencyCode));

            var acc = _accountsRepo.All.Single(a => a.Id == e.AccountId);
            if (acc.Type == AccountType.Assets && acc.CurrencyCode != e.Amount.CurrencyCode)
                throw new InvalidOperationException("Валюта проводки не совпадает с валютой счета");

            if (acc.Type == AccountType.Assets)
            {
                var d = e.Direction == EntryDirection.Debit ? e.Amount.Amount : -e.Amount.Amount;
                acc.Balance += d;
                SqliteConFactory.Exec(conn, "UPDATE Accounts SET Balance = @Bal, UpdatedAt = @Updated WHERE Id = @Id",
                    ("@Bal", (double)acc.Balance), ("@Updated", DateTimeOffset.Now.ToString("O")), ("@Id", acc.Id.ToString()));
            }
        }

        tran.Commit();

        _transactionsRepo.AddToCache(tx);   // fires Changed → RaiseChanged via subscription
        return Task.CompletedTask;
    }

    public Task StornoTransactionAsync(Guid transactionId)
    {
        var storno = _transactionsRepo.BuildStorno(transactionId, _accountsRepo.All);
        return PostTransactionAsync(storno);
    }
    public int GetLocalTransactionCount() => _transactionsRepo.GetLocalCount();

    public Task AddObligationAsync(Obligation obligation) => _obligationsRepo.Add(obligation);
    public Task UpdateObligationAsync(Obligation obligation) => _obligationsRepo.Update(obligation);
    public Task DeleteObligationAsync(Guid id) => _obligationsRepo.Delete(id);
    public Task MarkObligationPaidAsync(Guid id, bool isPaid) => _obligationsRepo.Mark(id, isPaid);

    public Task AddTemplateAsync(TransactionTemplate template) => _templatesRepo.Add(template);
    public Task DeleteTemplateAsync(Guid id) => _templatesRepo.Delete(id);

    public decimal GetRate(string fromCurrency, string toCurrency = "RUB") => _currencyRatesRepo.Get(fromCurrency, toCurrency);
    public void SetCurrencyRate(string code, decimal rate) => _currencyRatesRepo.Set(code, rate);

    public void ReplaceAllData(
        List<Account> accounts,
        List<Category> categories,
        List<Obligation> obligations,
        List<Transaction> transactions)
    {
        var accountGroupsMap = _accountsRepo.All.Where(a => a.GroupId.HasValue)
                                               .ToDictionary(a => a.Id, a => a.GroupId!.Value);

        using var conn = _factory.Open();
        using var tran = conn.BeginTransaction();

        SqliteConFactory.Exec(conn, "DELETE FROM Entries");
        SqliteConFactory.Exec(conn, "DELETE FROM Transactions");
        SqliteConFactory.Exec(conn, "DELETE FROM Obligations");
        SqliteConFactory.Exec(conn, "DELETE FROM Categories");
        SqliteConFactory.Exec(conn, "DELETE FROM Accounts");

        foreach (var a in accounts)
        {
            var groupId = accountGroupsMap.TryGetValue(a.Id, out var gId) ? (object)gId.ToString() : DBNull.Value;

            SqliteConFactory.Exec(conn, @"INSERT INTO Accounts (Id, Name, GroupId, CurrencyCode, InitialBalance, Balance, Type, AccountMultiType, SecondaryCurrencyCode, ExchangeRate, SecondaryBalance, IsDeleted, CreatedAt, UpdatedAt)
                         VALUES (@Id, @Name, @GroupId, @Cur, @Init, @Bal, @Type, @Multi, @Sec, @Rate, @SecBal, @Del, @Created, @Updated)",
                ("@Id", a.Id.ToString()),
                ("@Name", a.Name),
                ("@GroupId", groupId),
                ("@Cur", a.CurrencyCode),
                ("@Init", (double)a.InitialBalance),
                ("@Bal", (double)a.Balance),
                ("@Type", (int)a.Type),
                ("@Multi", (int)a.AccountMultiType),
                ("@Sec", (object?)a.SecondaryCurrencyCode ?? DBNull.Value),
                ("@Rate", a.ExchangeRate.HasValue ? (object)(double)a.ExchangeRate.Value : DBNull.Value),
                ("@SecBal", (double)a.SecondaryBalance),
                ("@Del", a.IsDeleted ? 1 : 0),
                ("@Created", a.CreatedAt.ToString("O")),
                ("@Updated", a.UpdatedAt.ToString("O")));
        }

        var now = DateTimeOffset.Now.ToString("O");
        foreach (var c in categories)
        {
            SqliteConFactory.Exec(conn, @"INSERT INTO Categories (Id, Name, Kind, CreatedAt, UpdatedAt)
                         VALUES (@Id, @Name, @Kind, @Now, @Now)",
                ("@Id", c.Id.ToString()),
                ("@Name", c.Name),
                ("@Kind", (int)c.Kind),
                ("@Now", now));
        }

        foreach (var o in obligations)
        {
            SqliteConFactory.Exec(conn, @"INSERT INTO Obligations (Id, Counterparty, Amount, Currency, Type, CreatedAt, DueDate, IsPaid, PaidAt, Note)
                         VALUES (@Id, @Cp, @Amt, @Cur, @Type, @Created, @Due, @Paid, @PaidAt, @Note)",
                ("@Id", o.Id.ToString()),
                ("@Cp", o.Counterparty),
                ("@Amt", (double)o.Amount),
                ("@Cur", o.Currency),
                ("@Type", (int)o.Type),
                ("@Created", o.CreatedAt.ToString("O")),
                ("@Due", o.DueDate.HasValue ? (object)o.DueDate.Value.ToString("O") : DBNull.Value),
                ("@Paid", o.IsPaid ? 1 : 0),
                ("@PaidAt", o.PaidAt.HasValue ? (object)o.PaidAt.Value.ToString("O") : DBNull.Value),
                ("@Note", (object?)o.Note ?? DBNull.Value));
        }

        foreach (var tx in transactions)
        {
            SqliteConFactory.Exec(conn, @"INSERT INTO Transactions (Id, Date, Description, CreatedAt)
                         VALUES (@Id, @Date, @Desc, @Now)",
                ("@Id", tx.Id.ToString()),
                ("@Date", tx.Date.ToString("O")),
                ("@Desc", tx.Description),
                ("@Now", now));

            foreach (var e in tx.Entries)
            {
                SqliteConFactory.Exec(conn, @"INSERT INTO Entries (Id, TransactionId, AccountId, CategoryId, Direction, Amount, CurrencyCode)
                             VALUES (@Id, @TxId, @AccId, @CatId, @Dir, @Amt, @Cur)",
                    ("@Id", e.Id.ToString()),
                    ("@TxId", tx.Id.ToString()),
                    ("@AccId", e.AccountId.ToString()),
                    ("@CatId", e.CategoryId.HasValue ? (object)e.CategoryId.Value.ToString() : DBNull.Value),
                    ("@Dir", (int)e.Direction),
                    ("@Amt", (double)e.Amount.Amount),
                    ("@Cur", e.Amount.CurrencyCode));
            }
        }

        tran.Commit();

        LoadAll();
        RaiseChanged();
    }

    private void LoadAll()
    {
        _categoriesRepo.Load();
        _accountsRepo.Load();
        _accountsRepo.RebuildTechnicalAccountMappings(_categoriesRepo.All);
        _transactionsRepo.Load();
        _obligationsRepo.Load();
        _templatesRepo.Load();
        _currencyRatesRepo.Load();
    }

    // Обёртка вокруг фабрики — оставлена для совместимости со всеми внутренними вызовами
    private static void Exec(SqliteConnection conn, string sql, params (string name, object value)[] parameters)
        => Client.Data.SqliteConFactory.Exec(conn, sql, parameters);

    public void ClearDatabase()
    {
        SqliteConnection.ClearAllPools();
        
        if (File.Exists(_factory.DbPath))
        {
            File.Delete(_factory.DbPath);
        }

        new DbInitializer(_factory).Initialize();
        LoadAll();

        RaiseChanged();
    }
}