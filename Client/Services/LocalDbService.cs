using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Client.Models;
using Microsoft.Data.Sqlite;

namespace Client.Services;

/// <summary>
/// IDataService на базе SQLite — оффлайн-хранилище.
/// БД: %APPDATA%/FinanceTracker/finance.db
/// </summary>
public sealed class LocalDbService : IDataService
{
    public event Action? DataChanged;
    private void RaiseChanged() => DataChanged?.Invoke();

    private readonly string _connectionString;

    // In-memory кэш
    private readonly List<Account> _accounts = new();
    private readonly List<Category> _categories = new();
    private readonly List<Transaction> _transactions = new();
    private readonly List<Obligation> _obligations = new();

    private readonly Dictionary<Guid, Guid> _expenseAccountByCategoryId = new();
    private readonly Dictionary<Guid, Guid> _incomeAccountByCategoryId = new();

    public IReadOnlyList<Account> Accounts => _accounts;
    public IReadOnlyList<Category> Categories => _categories;
    public IReadOnlyList<Transaction> Transactions => _transactions;
    public IReadOnlyList<Obligation> Obligations => _obligations;

    public LocalDbService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "FinanceTracker");
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "finance.db");

        _connectionString = $"Data Source={dbPath}";

        EnsureCreated();
        LoadAll();
    }

    // ───────────────────── Schema ─────────────────────

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    private void EnsureCreated()
    {
        using var conn = Open();

        Exec(conn, @"
            CREATE TABLE IF NOT EXISTS Accounts (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                CurrencyCode TEXT NOT NULL DEFAULT 'RUB',
                InitialBalance REAL NOT NULL DEFAULT 0,
                Balance REAL NOT NULL DEFAULT 0,
                Type INTEGER NOT NULL DEFAULT 0,
                AccountMultiType INTEGER NOT NULL DEFAULT 0,
                SecondaryCurrencyCode TEXT,
                ExchangeRate REAL,
                SecondaryBalance REAL NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            )");

        Exec(conn, @"
            CREATE TABLE IF NOT EXISTS Categories (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Kind INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            )");

        Exec(conn, @"
            CREATE TABLE IF NOT EXISTS Transactions (
                Id TEXT PRIMARY KEY,
                Date TEXT NOT NULL,
                Description TEXT NOT NULL DEFAULT '',
                CreatedAt TEXT NOT NULL
            )");

        Exec(conn, @"
            CREATE TABLE IF NOT EXISTS Entries (
                Id TEXT PRIMARY KEY,
                TransactionId TEXT NOT NULL,
                AccountId TEXT NOT NULL,
                CategoryId TEXT,
                Direction INTEGER NOT NULL,
                Amount REAL NOT NULL,
                CurrencyCode TEXT NOT NULL DEFAULT 'RUB',
                FOREIGN KEY (TransactionId) REFERENCES Transactions(Id),
                FOREIGN KEY (AccountId) REFERENCES Accounts(Id)
            )");

        Exec(conn, @"
            CREATE TABLE IF NOT EXISTS Obligations (
                Id TEXT PRIMARY KEY,
                Counterparty TEXT NOT NULL,
                Amount REAL NOT NULL DEFAULT 0,
                Currency TEXT NOT NULL DEFAULT 'RUB',
                Type INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                DueDate TEXT,
                IsPaid INTEGER NOT NULL DEFAULT 0,
                PaidAt TEXT,
                Note TEXT
            )");
    }

    // ───────────────────── Load ─────────────────────

    private void LoadAll()
    {
        _accounts.Clear();
        _categories.Clear();
        _transactions.Clear();
        _obligations.Clear();
        _expenseAccountByCategoryId.Clear();
        _incomeAccountByCategoryId.Clear();

        using var conn = Open();

        // Accounts
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT * FROM Accounts";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var acc = new Account
                {
                    Id = Guid.Parse(r.GetString(r.GetOrdinal("Id"))),
                    Name = r.GetString(r.GetOrdinal("Name")),
                    CurrencyCode = r.GetString(r.GetOrdinal("CurrencyCode")),
                    InitialBalance = (decimal)r.GetDouble(r.GetOrdinal("InitialBalance")),
                    Balance = (decimal)r.GetDouble(r.GetOrdinal("Balance")),
                    Type = (AccountType)r.GetInt32(r.GetOrdinal("Type")),
                    AccountMultiType = (MultiCurrencyType)r.GetInt32(r.GetOrdinal("AccountMultiType")),
                    SecondaryCurrencyCode = r.IsDBNull(r.GetOrdinal("SecondaryCurrencyCode")) ? null : r.GetString(r.GetOrdinal("SecondaryCurrencyCode")),
                    ExchangeRate = r.IsDBNull(r.GetOrdinal("ExchangeRate")) ? null : (decimal?)r.GetDouble(r.GetOrdinal("ExchangeRate")),
                    SecondaryBalance = (decimal)r.GetDouble(r.GetOrdinal("SecondaryBalance")),
                    CreatedAt = DateTimeOffset.Parse(r.GetString(r.GetOrdinal("CreatedAt"))),
                    UpdatedAt = DateTimeOffset.Parse(r.GetString(r.GetOrdinal("UpdatedAt")))
                };
                _accounts.Add(acc);

                // Восстанавливаем маппинг технических счетов
                if (acc.Type == AccountType.Expense && acc.Name.StartsWith("Расходы: "))
                {
                    var catId = FindCategoryIdByTechnicalAccountName(acc.Name, "Расходы: ");
                    if (catId.HasValue) _expenseAccountByCategoryId[catId.Value] = acc.Id;
                }
                else if (acc.Type == AccountType.Income && acc.Name.StartsWith("Доходы: "))
                {
                    var catId = FindCategoryIdByTechnicalAccountName(acc.Name, "Доходы: ");
                    if (catId.HasValue) _incomeAccountByCategoryId[catId.Value] = acc.Id;
                }
            }
        }

        // Categories
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT * FROM Categories";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                _categories.Add(new Category
                {
                    Id = Guid.Parse(r.GetString(r.GetOrdinal("Id"))),
                    Name = r.GetString(r.GetOrdinal("Name")),
                    Kind = (CategoryKind)r.GetInt32(r.GetOrdinal("Kind"))
                });
            }
        }

        // Починить маппинг технических счетов (после загрузки категорий)
        RebuildTechnicalAccountMappings();

        // Transactions + Entries
        var txMap = new Dictionary<Guid, Transaction>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT * FROM Transactions ORDER BY Date DESC";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var tx = new Transaction
                {
                    Id = Guid.Parse(r.GetString(r.GetOrdinal("Id"))),
                    Date = DateTimeOffset.Parse(r.GetString(r.GetOrdinal("Date"))),
                    Description = r.GetString(r.GetOrdinal("Description"))
                };
                _transactions.Add(tx);
                txMap[tx.Id] = tx;
            }
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT * FROM Entries";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var txId = Guid.Parse(r.GetString(r.GetOrdinal("TransactionId")));
                if (!txMap.TryGetValue(txId, out var tx)) continue;

                tx.Entries.Add(new Entry
                {
                    Id = Guid.Parse(r.GetString(r.GetOrdinal("Id"))),
                    AccountId = Guid.Parse(r.GetString(r.GetOrdinal("AccountId"))),
                    CategoryId = r.IsDBNull(r.GetOrdinal("CategoryId")) ? null : Guid.Parse(r.GetString(r.GetOrdinal("CategoryId"))),
                    Direction = (EntryDirection)r.GetInt32(r.GetOrdinal("Direction")),
                    Amount = new Money((decimal)r.GetDouble(r.GetOrdinal("Amount")), r.GetString(r.GetOrdinal("CurrencyCode")))
                });
            }
        }

        // Obligations
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT * FROM Obligations";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                _obligations.Add(new Obligation
                {
                    Id = Guid.Parse(r.GetString(r.GetOrdinal("Id"))),
                    Counterparty = r.GetString(r.GetOrdinal("Counterparty")),
                    Amount = (decimal)r.GetDouble(r.GetOrdinal("Amount")),
                    Currency = r.GetString(r.GetOrdinal("Currency")),
                    Type = (ObligationType)r.GetInt32(r.GetOrdinal("Type")),
                    CreatedAt = DateTimeOffset.Parse(r.GetString(r.GetOrdinal("CreatedAt"))),
                    DueDate = r.IsDBNull(r.GetOrdinal("DueDate")) ? null : DateTimeOffset.Parse(r.GetString(r.GetOrdinal("DueDate"))),
                    IsPaid = r.GetInt32(r.GetOrdinal("IsPaid")) == 1,
                    PaidAt = r.IsDBNull(r.GetOrdinal("PaidAt")) ? null : DateTimeOffset.Parse(r.GetString(r.GetOrdinal("PaidAt"))),
                    Note = r.IsDBNull(r.GetOrdinal("Note")) ? null : r.GetString(r.GetOrdinal("Note"))
                });
            }
        }
    }

    private Guid? FindCategoryIdByTechnicalAccountName(string accName, string prefix)
    {
        var catName = accName.Substring(prefix.Length);
        var cat = _categories.FirstOrDefault(c => c.Name == catName);
        return cat?.Id;
    }

    private void RebuildTechnicalAccountMappings()
    {
        foreach (var cat in _categories)
        {
            var expAcc = _accounts.FirstOrDefault(a => a.Type == AccountType.Expense && a.Name == $"Расходы: {cat.Name}");
            if (expAcc != null) _expenseAccountByCategoryId[cat.Id] = expAcc.Id;

            var incAcc = _accounts.FirstOrDefault(a => a.Type == AccountType.Income && a.Name == $"Доходы: {cat.Name}");
            if (incAcc != null) _incomeAccountByCategoryId[cat.Id] = incAcc.Id;
        }
    }

    // ───────────────────── IDataService: Accounts ─────────────────────

    public void AddAccount(Account account)
    {
        using var conn = Open();
        Exec(conn, @"INSERT INTO Accounts (Id, Name, CurrencyCode, InitialBalance, Balance, Type, AccountMultiType, SecondaryCurrencyCode, ExchangeRate, SecondaryBalance, CreatedAt, UpdatedAt)
                     VALUES (@Id, @Name, @Cur, @Init, @Bal, @Type, @Multi, @Sec, @Rate, @SecBal, @Created, @Updated)",
            ("@Id", account.Id.ToString()),
            ("@Name", account.Name),
            ("@Cur", account.CurrencyCode),
            ("@Init", (double)account.InitialBalance),
            ("@Bal", (double)account.Balance),
            ("@Type", (int)account.Type),
            ("@Multi", (int)account.AccountMultiType),
            ("@Sec", (object?)account.SecondaryCurrencyCode ?? DBNull.Value),
            ("@Rate", account.ExchangeRate.HasValue ? (object)(double)account.ExchangeRate.Value : DBNull.Value),
            ("@SecBal", (double)account.SecondaryBalance),
            ("@Created", account.CreatedAt.ToString("O")),
            ("@Updated", account.UpdatedAt.ToString("O")));

        _accounts.Add(account);
        RaiseChanged();
    }

    public void RenameAccount(Guid id, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return;
        var acc = _accounts.FirstOrDefault(a => a.Id == id);
        if (acc is null) return;

        acc.Name = newName.Trim();
        acc.UpdatedAt = DateTimeOffset.Now;

        using var conn = Open();
        Exec(conn, "UPDATE Accounts SET Name = @Name, UpdatedAt = @Updated WHERE Id = @Id",
            ("@Name", acc.Name), ("@Updated", acc.UpdatedAt.ToString("O")), ("@Id", id.ToString()));

        RaiseChanged();
    }

    public void RemoveAccount(Guid id)
    {
        var acc = _accounts.FirstOrDefault(a => a.Id == id);
        if (acc is null) return;

        using var conn = Open();
        Exec(conn, "DELETE FROM Accounts WHERE Id = @Id", ("@Id", id.ToString()));

        _accounts.Remove(acc);
        RaiseChanged();
    }

    public bool IsAccountUsed(Guid id) =>
        _transactions.Any(tx => tx.Entries.Any(e => e.AccountId == id));

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

    // ───────────────────── IDataService: Categories ─────────────────────

    public void AddCategory(Category category)
    {
        var now = DateTimeOffset.Now;
        using var conn = Open();
        Exec(conn, @"INSERT INTO Categories (Id, Name, Kind, CreatedAt, UpdatedAt)
                     VALUES (@Id, @Name, @Kind, @Created, @Updated)",
            ("@Id", category.Id.ToString()),
            ("@Name", category.Name),
            ("@Kind", (int)category.Kind),
            ("@Created", now.ToString("O")),
            ("@Updated", now.ToString("O")));

        _categories.Add(category);

        // Создать технические счета
        CreateTechnicalAccountsForCategory(conn, category);

        RaiseChanged();
    }

    public void RemoveCategory(Category category)
    {
        using var conn = Open();
        Exec(conn, "DELETE FROM Categories WHERE Id = @Id", ("@Id", category.Id.ToString()));

        _categories.RemoveAll(c => c.Id == category.Id);
        RaiseChanged();
    }

    private void CreateTechnicalAccountsForCategory(SqliteConnection conn, Category cat)
    {
        var now = DateTimeOffset.Now;
        var expAcc = new Account
        {
            Name = $"Расходы: {cat.Name}",
            CurrencyCode = "RUB",
            Balance = 0,
            Type = AccountType.Expense,
            CreatedAt = now,
            UpdatedAt = now
        };

        Exec(conn, @"INSERT INTO Accounts (Id, Name, CurrencyCode, InitialBalance, Balance, Type, AccountMultiType, SecondaryCurrencyCode, ExchangeRate, SecondaryBalance, CreatedAt, UpdatedAt)
                     VALUES (@Id, @Name, 'RUB', 0, 0, @Type, 0, NULL, NULL, 0, @Created, @Updated)",
            ("@Id", expAcc.Id.ToString()), ("@Name", expAcc.Name),
            ("@Type", (int)AccountType.Expense),
            ("@Created", now.ToString("O")), ("@Updated", now.ToString("O")));
        _accounts.Add(expAcc);
        _expenseAccountByCategoryId[cat.Id] = expAcc.Id;

        var incAcc = new Account
        {
            Name = $"Доходы: {cat.Name}",
            CurrencyCode = "RUB",
            Balance = 0,
            Type = AccountType.Income,
            CreatedAt = now,
            UpdatedAt = now
        };

        Exec(conn, @"INSERT INTO Accounts (Id, Name, CurrencyCode, InitialBalance, Balance, Type, AccountMultiType, SecondaryCurrencyCode, ExchangeRate, SecondaryBalance, CreatedAt, UpdatedAt)
                     VALUES (@Id, @Name, 'RUB', 0, 0, @Type, 0, NULL, NULL, 0, @Created, @Updated)",
            ("@Id", incAcc.Id.ToString()), ("@Name", incAcc.Name),
            ("@Type", (int)AccountType.Income),
            ("@Created", now.ToString("O")), ("@Updated", now.ToString("O")));
        _accounts.Add(incAcc);
        _incomeAccountByCategoryId[cat.Id] = incAcc.Id;
    }

    // ───────────────────── IDataService: Transactions ─────────────────────

    public Task PostTransactionAsync(Transaction tx)
    {
        if (tx.Entries.Count < 2)
            throw new InvalidOperationException("Транзакция не содержит двух проводок");

        using var conn = Open();
        using var tran = conn.BeginTransaction();

        Exec(conn, @"INSERT INTO Transactions (Id, Date, Description, CreatedAt)
                     VALUES (@Id, @Date, @Desc, @Created)",
            ("@Id", tx.Id.ToString()),
            ("@Date", tx.Date.ToString("O")),
            ("@Desc", tx.Description),
            ("@Created", DateTimeOffset.Now.ToString("O")));

        foreach (var e in tx.Entries)
        {
            Exec(conn, @"INSERT INTO Entries (Id, TransactionId, AccountId, CategoryId, Direction, Amount, CurrencyCode)
                         VALUES (@Id, @TxId, @AccId, @CatId, @Dir, @Amt, @Cur)",
                ("@Id", e.Id.ToString()),
                ("@TxId", tx.Id.ToString()),
                ("@AccId", e.AccountId.ToString()),
                ("@CatId", e.CategoryId.HasValue ? (object)e.CategoryId.Value.ToString() : DBNull.Value),
                ("@Dir", (int)e.Direction),
                ("@Amt", (double)e.Amount.Amount),
                ("@Cur", e.Amount.CurrencyCode));

            // Обновить баланс
            var acc = _accounts.Single(a => a.Id == e.AccountId);
            if (acc.CurrencyCode != e.Amount.CurrencyCode)
                throw new InvalidOperationException("Валюта проводки не совпадает с валютой счета");

            if (acc.Type == AccountType.Assets)
            {
                var d = e.Direction == EntryDirection.Debit ? e.Amount.Amount : -e.Amount.Amount;
                acc.Balance += d;
                Exec(conn, "UPDATE Accounts SET Balance = @Bal, UpdatedAt = @Updated WHERE Id = @Id",
                    ("@Bal", (double)acc.Balance), ("@Updated", DateTimeOffset.Now.ToString("O")), ("@Id", acc.Id.ToString()));
            }
        }

        tran.Commit();
        _transactions.Insert(0, tx);
        RaiseChanged();
        return Task.CompletedTask;
    }

    // ───────────────────── IDataService: Obligations ─────────────────────

    public Task AddObligationAsync(Obligation obligation)
    {
        using var conn = Open();
        Exec(conn, @"INSERT INTO Obligations (Id, Counterparty, Amount, Currency, Type, CreatedAt, DueDate, IsPaid, PaidAt, Note)
                     VALUES (@Id, @Cp, @Amt, @Cur, @Type, @Created, @Due, @Paid, @PaidAt, @Note)",
            ("@Id", obligation.Id.ToString()),
            ("@Cp", obligation.Counterparty),
            ("@Amt", (double)obligation.Amount),
            ("@Cur", obligation.Currency),
            ("@Type", (int)obligation.Type),
            ("@Created", obligation.CreatedAt.ToString("O")),
            ("@Due", obligation.DueDate.HasValue ? (object)obligation.DueDate.Value.ToString("O") : DBNull.Value),
            ("@Paid", obligation.IsPaid ? 1 : 0),
            ("@PaidAt", obligation.PaidAt.HasValue ? (object)obligation.PaidAt.Value.ToString("O") : DBNull.Value),
            ("@Note", (object?)obligation.Note ?? DBNull.Value));

        _obligations.Add(obligation);
        RaiseChanged();
        return Task.CompletedTask;
    }

    public Task UpdateObligationAsync(Obligation obligation)
    {
        using var conn = Open();
        Exec(conn, @"UPDATE Obligations SET Counterparty=@Cp, Amount=@Amt, Currency=@Cur, Type=@Type,
                     DueDate=@Due, IsPaid=@Paid, PaidAt=@PaidAt, Note=@Note WHERE Id=@Id",
            ("@Cp", obligation.Counterparty),
            ("@Amt", (double)obligation.Amount),
            ("@Cur", obligation.Currency),
            ("@Type", (int)obligation.Type),
            ("@Due", obligation.DueDate.HasValue ? (object)obligation.DueDate.Value.ToString("O") : DBNull.Value),
            ("@Paid", obligation.IsPaid ? 1 : 0),
            ("@PaidAt", obligation.PaidAt.HasValue ? (object)obligation.PaidAt.Value.ToString("O") : DBNull.Value),
            ("@Note", (object?)obligation.Note ?? DBNull.Value),
            ("@Id", obligation.Id.ToString()));

        var idx = _obligations.FindIndex(o => o.Id == obligation.Id);
        if (idx >= 0) _obligations[idx] = obligation;
        RaiseChanged();
        return Task.CompletedTask;
    }

    public Task DeleteObligationAsync(Guid id)
    {
        using var conn = Open();
        Exec(conn, "DELETE FROM Obligations WHERE Id = @Id", ("@Id", id.ToString()));

        _obligations.RemoveAll(o => o.Id == id);
        RaiseChanged();
        return Task.CompletedTask;
    }

    public Task MarkObligationPaidAsync(Guid id, bool isPaid)
    {
        var existing = _obligations.FirstOrDefault(o => o.Id == id);
        if (existing == null) return Task.CompletedTask;

        existing.IsPaid = isPaid;
        existing.PaidAt = isPaid ? DateTimeOffset.Now : null;

        using var conn = Open();
        Exec(conn, "UPDATE Obligations SET IsPaid=@Paid, PaidAt=@PaidAt WHERE Id=@Id",
            ("@Paid", isPaid ? 1 : 0),
            ("@PaidAt", existing.PaidAt.HasValue ? (object)existing.PaidAt.Value.ToString("O") : DBNull.Value),
            ("@Id", id.ToString()));

        RaiseChanged();
        return Task.CompletedTask;
    }

    // ───────────────────── Sync: Replace All Data ─────────────────────

    /// <summary>
    /// Заменить все данные в SQLite на серверные (pull sync).
    /// </summary>
    public void ReplaceAllData(
        List<Account> accounts,
        List<Category> categories,
        List<Obligation> obligations,
        List<Transaction> transactions)
    {
        using var conn = Open();
        using var tran = conn.BeginTransaction();

        // Очистить все таблицы
        Exec(conn, "DELETE FROM Entries");
        Exec(conn, "DELETE FROM Transactions");
        Exec(conn, "DELETE FROM Obligations");
        Exec(conn, "DELETE FROM Categories");
        Exec(conn, "DELETE FROM Accounts");

        // Вставить счета
        foreach (var a in accounts)
        {
            Exec(conn, @"INSERT INTO Accounts (Id, Name, CurrencyCode, InitialBalance, Balance, Type, AccountMultiType, SecondaryCurrencyCode, ExchangeRate, SecondaryBalance, CreatedAt, UpdatedAt)
                         VALUES (@Id, @Name, @Cur, @Init, @Bal, @Type, @Multi, @Sec, @Rate, @SecBal, @Created, @Updated)",
                ("@Id", a.Id.ToString()),
                ("@Name", a.Name),
                ("@Cur", a.CurrencyCode),
                ("@Init", (double)a.InitialBalance),
                ("@Bal", (double)a.Balance),
                ("@Type", (int)a.Type),
                ("@Multi", (int)a.AccountMultiType),
                ("@Sec", (object?)a.SecondaryCurrencyCode ?? DBNull.Value),
                ("@Rate", a.ExchangeRate.HasValue ? (object)(double)a.ExchangeRate.Value : DBNull.Value),
                ("@SecBal", (double)a.SecondaryBalance),
                ("@Created", a.CreatedAt.ToString("O")),
                ("@Updated", a.UpdatedAt.ToString("O")));
        }

        // Вставить категории
        var now = DateTimeOffset.Now;
        foreach (var c in categories)
        {
            Exec(conn, @"INSERT INTO Categories (Id, Name, Kind, CreatedAt, UpdatedAt)
                         VALUES (@Id, @Name, @Kind, @Created, @Updated)",
                ("@Id", c.Id.ToString()),
                ("@Name", c.Name),
                ("@Kind", (int)c.Kind),
                ("@Created", now.ToString("O")),
                ("@Updated", now.ToString("O")));
        }

        // Вставить транзакции и проводки
        foreach (var tx in transactions)
        {
            Exec(conn, @"INSERT INTO Transactions (Id, Date, Description, CreatedAt)
                         VALUES (@Id, @Date, @Desc, @Created)",
                ("@Id", tx.Id.ToString()),
                ("@Date", tx.Date.ToString("O")),
                ("@Desc", tx.Description),
                ("@Created", now.ToString("O")));

            foreach (var e in tx.Entries)
            {
                Exec(conn, @"INSERT INTO Entries (Id, TransactionId, AccountId, CategoryId, Direction, Amount, CurrencyCode)
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

        // Вставить обязательства
        foreach (var o in obligations)
        {
            Exec(conn, @"INSERT INTO Obligations (Id, Counterparty, Amount, Currency, Type, CreatedAt, DueDate, IsPaid, PaidAt, Note)
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

        tran.Commit();

        // Перезагрузить in-memory кэш
        LoadAll();
        RaiseChanged();
    }

    // ───────────────────── Helpers ─────────────────────

    private static void Exec(SqliteConnection conn, string sql, params (string name, object value)[] parameters)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value);
        cmd.ExecuteNonQuery();
    }
}
