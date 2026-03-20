using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Client.Models;
using Microsoft.Data.Sqlite;

namespace Client.Services;

public sealed class LocalDbService : IDataService   // Основа - SQLite
{
    public event Action? DataChanged;
    private void RaiseChanged() => DataChanged?.Invoke();

    private readonly string _connectionString;
    private readonly string _dbPath;

    private readonly List<Account> _accounts = new();
    private readonly List<Category> _categories = new();
    private readonly List<Transaction> _transactions = new();
    private readonly List<Obligation> _obligations = new();

    private readonly Dictionary<Guid, Guid> _expenseAccountByCategoryId = new();
    private readonly Dictionary<Guid, Guid> _incomeAccountByCategoryId = new();
    private readonly List<CurrencyRate> _currencyRates = new();

    public IReadOnlyList<Account> Accounts => _accounts;
    public IReadOnlyList<Category> Categories => _categories;
    public IReadOnlyList<Transaction> Transactions => _transactions;
    public IReadOnlyList<Obligation> Obligations => _obligations;
    public IReadOnlyList<CurrencyRate> CurrencyRates => _currencyRates;

    public LocalDbService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "Diplom");
        Directory.CreateDirectory(dir);
        _dbPath = Path.Combine(dir, "finance.db");

        _connectionString = $"Data Source={_dbPath}";

        EnsureCreated();
        MigrateSchema();
        LoadAll();
    }

private SqliteConnection Open() // соединение с локальной бд
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    private void EnsureCreated()    // инициализация бд
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
                IsDeleted INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            )");    // Счета

        Exec(conn, @"
            CREATE TABLE IF NOT EXISTS Categories (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Kind INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            )");    // Категории

        Exec(conn, @"
            CREATE TABLE IF NOT EXISTS Transactions (
                Id TEXT PRIMARY KEY,
                Date TEXT NOT NULL,
                Description TEXT NOT NULL DEFAULT '',
                CreatedAt TEXT NOT NULL
            )");    // Транзакции

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
            )");    // Проводки

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
            )");    // Обязательства

        Exec(conn, @"
            CREATE TABLE IF NOT EXISTS CurrencyRates (
                CurrencyCode TEXT PRIMARY KEY,
                RateToBase REAL NOT NULL
            )");    // Курсы валют
            
        SeedDataIfEmpty(conn);
    }

    private void MigrateSchema()
    {
        using var conn = Open();
        
        // Проверка наличия колонки IsDeleted в Accounts
        bool hasIsDeleted = false;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(Accounts)";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (reader.GetString(reader.GetOrdinal("name")).Equals("IsDeleted", StringComparison.OrdinalIgnoreCase))
                {
                    hasIsDeleted = true;
                    break;
                }
            }
        }

        if (!hasIsDeleted)
        {
            Exec(conn, "ALTER TABLE Accounts ADD COLUMN IsDeleted INTEGER NOT NULL DEFAULT 0");
        }
    }

    private void SeedDataIfEmpty(SqliteConnection conn) // демо-данные
    {
        using var cmd = conn.CreateCommand();
        
        cmd.CommandText = "SELECT COUNT(*) FROM CurrencyRates";
        var ratesCount = (long)cmd.ExecuteScalar()!;
        if (ratesCount == 0)
        {
            var defaultRates = new Dictionary<string, double>
            {
                { "RUB", 1.0 }, { "USD", 72.0 }, { "EUR", 91.0 }, { "KZT", 0.16 },
                { "GBP", 104.0 }, { "CNY", 12.0 }, { "TRY", 1.8 }
            };  // курсы валют (пока фиксированный для рубля)

            foreach (var kvp in defaultRates)
            {
                Exec(conn, "INSERT INTO CurrencyRates (CurrencyCode, RateToBase) VALUES (@Code, @Rate)",
                    ("@Code", kvp.Key), ("@Rate", kvp.Value));
            }
        }

        cmd.CommandText = "SELECT COUNT(*) FROM Accounts";
        var accsCount = (long)cmd.ExecuteScalar()!;
        if (accsCount == 0)
        {
            var now = DateTimeOffset.Now.ToString("O");

            var catSalaryId = Guid.NewGuid().ToString();
            var accIncomeSalaryId = Guid.NewGuid().ToString();
            var accExpenseSalaryId = Guid.NewGuid().ToString();
            
            var catFoodId = Guid.NewGuid().ToString();
            var accIncomeFoodId = Guid.NewGuid().ToString();
            var accExpenseFoodId = Guid.NewGuid().ToString();

            var catTransportId = Guid.NewGuid().ToString();
            var accIncomeTransportId = Guid.NewGuid().ToString();
            var accExpenseTransportId = Guid.NewGuid().ToString();

            void AddCategory(string id, string name, int kind, string incomeId, string expenseId)
            {
                Exec(conn, @"INSERT INTO Categories (Id, Name, Kind, CreatedAt, UpdatedAt) VALUES (@Id, @Name, @Kind, @Now, @Now)", 
                    ("@Id", id), ("@Name", name), ("@Kind", kind), ("@Now", now));
                
                Exec(conn, @"INSERT INTO Accounts (Id, Name, CurrencyCode, InitialBalance, Balance, Type, AccountMultiType, SecondaryBalance, CreatedAt, UpdatedAt)
                             VALUES (@Id, @Name, 'RUB', 0, 0, 2, 0, 0, @Now, @Now)",
                             ("@Id", expenseId), ("@Name", $"Расходы: {name}"), ("@Now", now));
                
                Exec(conn, @"INSERT INTO Accounts (Id, Name, CurrencyCode, InitialBalance, Balance, Type, AccountMultiType, SecondaryBalance, CreatedAt, UpdatedAt)
                             VALUES (@Id, @Name, 'RUB', 0, 0, 1, 0, 0, @Now, @Now)",
                             ("@Id", incomeId), ("@Name", $"Доходы: {name}"), ("@Now", now));
            }

            AddCategory(catSalaryId, "Зарплата", 1, accIncomeSalaryId, accExpenseSalaryId);
            AddCategory(catFoodId, "Продукты", 0, accIncomeFoodId, accExpenseFoodId);
            AddCategory(catTransportId, "Транспорт", 0, accIncomeTransportId, accExpenseTransportId);
            
            var act1 = Guid.NewGuid().ToString();
            var act2 = Guid.NewGuid().ToString();
            
            Exec(conn, @"INSERT INTO Accounts (Id, Name, CurrencyCode, InitialBalance, Balance, Type, AccountMultiType, SecondaryBalance, CreatedAt, UpdatedAt) 
                         VALUES (@Id, 'Наличные', 'RUB', 50000, 39500, 0, 0, 0, @Now, @Now)",
                         ("@Id", act1), ("@Now", now));
            Exec(conn, @"INSERT INTO Accounts (Id, Name, CurrencyCode, InitialBalance, Balance, Type, AccountMultiType, SecondaryBalance, CreatedAt, UpdatedAt) 
                         VALUES (@Id, 'Заначка USD', 'USD', 1000, 1000, 0, 0, 0, @Now, @Now)",
                         ("@Id", act2), ("@Now", now));

            Exec(conn, @"INSERT INTO Obligations (Id, Counterparty, Amount, Currency, Type, CreatedAt, IsPaid)
                         VALUES (@Id, 'Иван', 5000, 'RUB', 0, @Now, 0)",
                         ("@Id", Guid.NewGuid().ToString()), ("@Now", now));

            void AddTx(string dateDelta, string desc, string accId, string catId, string techAccId, EntryDirection mainAccDir, double amount, string currency)
            {
                var txId = Guid.NewGuid().ToString();
                var txDate = DateTimeOffset.Now.AddDays(int.Parse(dateDelta)).ToString("O");
                
                Exec(conn, @"INSERT INTO Transactions (Id, Date, Description, CreatedAt) VALUES (@Id, @Date, @Desc, @Now)",
                    ("@Id", txId), ("@Date", txDate), ("@Desc", desc), ("@Now", now));
                
                var e1Id = Guid.NewGuid().ToString();
                Exec(conn, @"INSERT INTO Entries (Id, TransactionId, AccountId, CategoryId, Direction, Amount, CurrencyCode)
                             VALUES (@Id, @TxId, @AccId, @CatId, @Dir, @Amt, @Cur)",
                             ("@Id", e1Id), ("@TxId", txId), ("@AccId", accId), ("@CatId", catId), 
                             ("@Dir", (int)mainAccDir), ("@Amt", amount), ("@Cur", currency));
                             
                var e2Id = Guid.NewGuid().ToString();
                var techDir = mainAccDir == EntryDirection.Debit ? EntryDirection.Credit : EntryDirection.Debit;
                Exec(conn, @"INSERT INTO Entries (Id, TransactionId, AccountId, CategoryId, Direction, Amount, CurrencyCode)
                             VALUES (@Id, @TxId, @AccId, @CatId, @Dir, @Amt, @Cur)",
                             ("@Id", e2Id), ("@TxId", txId), ("@AccId", techAccId), ("@CatId", catId), 
                             ("@Dir", (int)techDir), ("@Amt", amount), ("@Cur", currency));
            }

            AddTx("-5", "Аванс", act1, catSalaryId, accIncomeSalaryId, EntryDirection.Debit, 50000, "RUB");
            AddTx("-2", "Лента", act1, catFoodId, accExpenseFoodId, EntryDirection.Credit, 8500, "RUB");
            AddTx("-1", "Пятерочка", act1, catFoodId, accExpenseFoodId, EntryDirection.Credit, 1200, "RUB");
            AddTx("0", "Такси", act1, catTransportId, accExpenseTransportId, EntryDirection.Credit, 800, "RUB");
        }
    }

    private void LoadAll()  // Сохранение из БД в ОЗУ
    {
        // Очистка текущих кэшей 
        _accounts.Clear();
        _categories.Clear();
        _transactions.Clear();
        _obligations.Clear();
        _currencyRates.Clear();
        _expenseAccountByCategoryId.Clear();
        _incomeAccountByCategoryId.Clear();

        using var conn = Open();

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
                    IsDeleted = r.GetInt32(r.GetOrdinal("IsDeleted")) == 1,
                    CreatedAt = DateTimeOffset.Parse(r.GetString(r.GetOrdinal("CreatedAt"))),
                    UpdatedAt = DateTimeOffset.Parse(r.GetString(r.GetOrdinal("UpdatedAt")))
                };
                _accounts.Add(acc);

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

        RebuildTechnicalAccountMappings();

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

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT * FROM CurrencyRates";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                _currencyRates.Add(new CurrencyRate
                {
                    CurrencyCode = r.GetString(r.GetOrdinal("CurrencyCode")),
                    RateToBase = (decimal)r.GetDouble(r.GetOrdinal("RateToBase"))
                });
            }
        }
    }

    private Guid? FindCategoryIdByTechnicalAccountName(string accName, string prefix)   // Поиск категории
    {
        var catName = accName.Substring(prefix.Length);
        var cat = _categories.FirstOrDefault(c => c.Name == catName);
        return cat?.Id;
    }

    private void RebuildTechnicalAccountMappings()  // Восставновление категория - название
    {
        foreach (var cat in _categories)
        {
            var expAcc = _accounts.FirstOrDefault(a => a.Type == AccountType.Expense && a.Name == $"Расходы: {cat.Name}");
            if (expAcc != null) _expenseAccountByCategoryId[cat.Id] = expAcc.Id;

            var incAcc = _accounts.FirstOrDefault(a => a.Type == AccountType.Income && a.Name == $"Доходы: {cat.Name}");
            if (incAcc != null) _incomeAccountByCategoryId[cat.Id] = incAcc.Id;
        }
    }

    public void AddAccount(Account account) // Добавление счета
    {
        using var conn = Open();
        
        Exec(conn, @"INSERT INTO Accounts (Id, Name, CurrencyCode, InitialBalance, Balance, Type, AccountMultiType, SecondaryCurrencyCode, ExchangeRate, SecondaryBalance, IsDeleted, CreatedAt, UpdatedAt)
                     VALUES (@Id, @Name, @Cur, @Init, @Bal, @Type, @Multi, @Sec, @Rate, @SecBal, @IsDeleted, @Created, @Updated)",
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
            ("@IsDeleted", account.IsDeleted ? 1 : 0),
            ("@Created", account.CreatedAt.ToString("O")),
            ("@Updated", account.UpdatedAt.ToString("O")));

        _accounts.Add(account);
        RaiseChanged();
    }

    public void RenameAccount(Guid id, string newName)  // Переименовывание счета
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

    public void RemoveAccount(Guid id)  // soft-delete счета
    {
        var acc = _accounts.FirstOrDefault(a => a.Id == id);
        if (acc is null) return;

        acc.IsDeleted = true;
        acc.UpdatedAt = DateTimeOffset.Now;

        using var conn = Open();
        Exec(conn, "UPDATE Accounts SET IsDeleted = 1, UpdatedAt = @Updated WHERE Id = @Id", 
            ("@Updated", acc.UpdatedAt.ToString("O")), ("@Id", id.ToString()));

        RaiseChanged();
    }

    public bool IsAccountUsed(Guid id) => _transactions.Any(tx => tx.Entries.Any(e => e.AccountId == id));  // Был ли использован счет 

    public DateTimeOffset? GetLocalLastChangeDate()
    {
        var dates = new List<DateTimeOffset>();
        if (_accounts.Count > 0) dates.Add(_accounts.Max(a => a.UpdatedAt));
        if (_transactions.Count > 0) dates.Add(_transactions.Max(t => t.Date));
        if (_obligations.Count > 0) dates.Add(_obligations.Max(o => o.CreatedAt));
        return dates.Count > 0 ? dates.Max() : null;
    }

    public int GetLocalTransactionCount() => _transactions.Count;

    public Account GetExpenseAccountForCategory(Guid categoryId)    // Категория - расход
    {
        if (!_expenseAccountByCategoryId.TryGetValue(categoryId, out var accId))
            throw new InvalidOperationException($"Expense account not found for category {categoryId}");
        return _accounts.Single(a => a.Id == accId);
    }

    public Account GetIncomeAccountForCategory(Guid categoryId) // Категория - доход
    {
        if (!_incomeAccountByCategoryId.TryGetValue(categoryId, out var accId))
            throw new InvalidOperationException($"Income account not found for category {categoryId}");
        return _accounts.Single(a => a.Id == accId);
    }

    public void AddCategory(Category category)  // Добавление категории
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

        CreateTechnicalAccountsForCategory(conn, category);
        RaiseChanged();
    }

    public void RemoveCategory(Category category)   // Удаление категории
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

    public Task PostTransactionAsync(Transaction tx)    // Запись транзакции
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

            // Обновление баланса счета, затронутого проводкой
            var acc = _accounts.Single(a => a.Id == e.AccountId);
            if (acc.Type == AccountType.Assets && acc.CurrencyCode != e.Amount.CurrencyCode)
                throw new InvalidOperationException("Валюта проводки не совпадает с валютой счета");

            if (acc.Type == AccountType.Assets)
            {
                var d = e.Direction == EntryDirection.Debit ? e.Amount.Amount : -e.Amount.Amount;
                acc.Balance += d;
                Exec(conn, "UPDATE Accounts SET Balance = @Bal, UpdatedAt = @Updated WHERE Id = @Id",
                    ("@Bal", (double)acc.Balance), ("@Updated", DateTimeOffset.Now.ToString("O")), ("@Id", acc.Id.ToString()));
            }
        }

        tran.Commit();  // Отправление изменений в бд
        
        _transactions.Insert(0, tx);
        RaiseChanged();
        return Task.CompletedTask;
    }

    public Task StornoTransactionAsync(Guid transactionId)  // Сторнирование транзакции
    {
        var tx = _transactions.FirstOrDefault(t => t.Id == transactionId);
        if (tx == null) return Task.CompletedTask;

        foreach (var e in tx.Entries)
        {
            var acc = _accounts.FirstOrDefault(a => a.Id == e.AccountId);
            if (acc != null && acc.Type == AccountType.Assets && acc.IsDeleted)
            {
                throw new InvalidOperationException("Нельзя сторнировать транзакцию: один из связанных счетов удален.");
            }
        }

        var stornoTx = new Transaction
        {
            Id = Guid.NewGuid(),
            Date = DateTimeOffset.Now,
            Description = $"[СТОРНО] {tx.Description}".Trim(),
            Entries = new System.Collections.Generic.List<Entry>()
        };

        foreach (var e in tx.Entries)
        {
            stornoTx.Entries.Add(new Entry
            {
                Id = Guid.NewGuid(),
                AccountId = e.AccountId,
                CategoryId = e.CategoryId,
                Direction = e.Direction == EntryDirection.Debit ? EntryDirection.Credit : EntryDirection.Debit,
                Amount = new Money(e.Amount.Amount, e.Amount.CurrencyCode)
            });
        }

        return PostTransactionAsync(stornoTx);
    }

    public Task AddObligationAsync(Obligation obligation)   // Добавление обязательтсва
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

    public Task UpdateObligationAsync(Obligation obligation)    // Изменение обязательства
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

    public Task DeleteObligationAsync(Guid id)  // Удаление обязатлеьства
    {
        using var conn = Open();
        Exec(conn, "DELETE FROM Obligations WHERE Id = @Id", ("@Id", id.ToString()));

        _obligations.RemoveAll(o => o.Id == id);
        RaiseChanged();
        return Task.CompletedTask;
    }

    public Task MarkObligationPaidAsync(Guid id, bool isPaid)   // Пометка обязательства
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

    public decimal GetRate(string fromCurrency, string toCurrency = "RUB")  // Получение курса валют
    {
        if (fromCurrency == toCurrency) return 1m;
        
        // Для простоты пока считаем, что все кросскурсы идут через базовую валюту
        var fromRate = fromCurrency == "RUB" ? 1m : _currencyRates.FirstOrDefault(r => r.CurrencyCode == fromCurrency)?.RateToBase ?? 1m;
        var toRate = toCurrency == "RUB" ? 1m : _currencyRates.FirstOrDefault(r => r.CurrencyCode == toCurrency)?.RateToBase ?? 1m;

        if (toRate == 0) return 0;
        return fromRate / toRate;
    }

    public void SetCurrencyRate(string code, decimal rate)  // Обновление курса валют
    {
        if (code == "RUB") return;

        var existing = _currencyRates.FirstOrDefault(r => r.CurrencyCode == code);
        using var conn = Open();

        if (existing != null)
        {
            existing.RateToBase = rate;
            Exec(conn, "UPDATE CurrencyRates SET RateToBase = @Rate WHERE CurrencyCode = @Code",
                ("@Rate", (double)rate), ("@Code", code));
        }
        else
        {
            var newRate = new CurrencyRate { CurrencyCode = code, RateToBase = rate };
            _currencyRates.Add(newRate);
            Exec(conn, "INSERT INTO CurrencyRates (CurrencyCode, RateToBase) VALUES (@Code, @Rate)",
                ("@Code", code), ("@Rate", (double)rate));
        }

        RaiseChanged();
    }

    /// <summary>
    /// Полная замена локальных данных (после синхронизации с сервером).
    /// Удаляет все существующие записи и вставляет новые.
    /// </summary>
    public void ReplaceAllData(
        List<Account> accounts,
        List<Category> categories,
        List<Obligation> obligations,
        List<Transaction> transactions)
    {
        using var conn = Open();
        using var tran = conn.BeginTransaction();

        // 1. Очистить все таблицы
        Exec(conn, "DELETE FROM Entries");
        Exec(conn, "DELETE FROM Transactions");
        Exec(conn, "DELETE FROM Obligations");
        Exec(conn, "DELETE FROM Categories");
        Exec(conn, "DELETE FROM Accounts");

        // 2. Вставить счета
        foreach (var a in accounts)
        {
            Exec(conn, @"INSERT INTO Accounts (Id, Name, CurrencyCode, InitialBalance, Balance, Type, AccountMultiType, SecondaryCurrencyCode, ExchangeRate, SecondaryBalance, IsDeleted, CreatedAt, UpdatedAt)
                         VALUES (@Id, @Name, @Cur, @Init, @Bal, @Type, @Multi, @Sec, @Rate, @SecBal, @Del, @Created, @Updated)",
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
                ("@Del", a.IsDeleted ? 1 : 0),
                ("@Created", a.CreatedAt.ToString("O")),
                ("@Updated", a.UpdatedAt.ToString("O")));
        }

        // 3. Вставить категории
        var now = DateTimeOffset.Now.ToString("O");
        foreach (var c in categories)
        {
            Exec(conn, @"INSERT INTO Categories (Id, Name, Kind, CreatedAt, UpdatedAt)
                         VALUES (@Id, @Name, @Kind, @Now, @Now)",
                ("@Id", c.Id.ToString()),
                ("@Name", c.Name),
                ("@Kind", (int)c.Kind),
                ("@Now", now));
        }

        // 4. Вставить обязательства
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

        // 5. Вставить транзакции и проводки
        foreach (var tx in transactions)
        {
            Exec(conn, @"INSERT INTO Transactions (Id, Date, Description, CreatedAt)
                         VALUES (@Id, @Date, @Desc, @Now)",
                ("@Id", tx.Id.ToString()),
                ("@Date", tx.Date.ToString("O")),
                ("@Desc", tx.Description),
                ("@Now", now));

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

        tran.Commit();

        // 6. Перезагрузить кэш из БД
        LoadAll();
        RaiseChanged();
    }

    /// <param name="conn">Соединение с БД.</param>
    /// <param name="sql">SQL-строка запроса.</param>
    /// <param name="parameters">Параметры для защиты от SQL-инъекций.</param>
    private static void Exec(SqliteConnection conn, string sql, params (string name, object value)[] parameters)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value);
        cmd.ExecuteNonQuery();
    }

    public void ClearDatabase()
    {
        SqliteConnection.ClearAllPools();
        
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }

        _accounts.Clear();
        _categories.Clear();
        _transactions.Clear();
        _obligations.Clear();
        _currencyRates.Clear();
        _expenseAccountByCategoryId.Clear();
        _incomeAccountByCategoryId.Clear();

        EnsureCreated();
        MigrateSchema();
        LoadAll();
        
        RaiseChanged();
    }
}