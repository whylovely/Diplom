using System;
using Microsoft.Data.Sqlite;

namespace Client.Data;

/// <summary>
/// Создаёт схему БД при первом запуске и доливает недостающие колонки
/// при обновлении приложения. Вызывается один раз из конструктора <c>LocalDbService</c>.
/// </summary>
public sealed class DbInitializer
{
    private readonly SqliteConFactory _factory;
    public DbInitializer(SqliteConFactory f) => _factory = f;

    public void Initialize()
    {
        EnsureCreated();
        MigrateSchema();
    }

    /// Создаёт все таблицы
    private void EnsureCreated()
    {
        using var conn = _factory.Open();

        // Счета
        SqliteConFactory.Exec(conn, @"
            CREATE TABLE IF NOT EXISTS Accounts (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                GroupId TEXT,
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
            )");

        // Группы счетов
        SqliteConFactory.Exec(conn, @"
            CREATE TABLE IF NOT EXISTS AccountGroups (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                SortOrder INTEGER NOT NULL DEFAULT 0
            )");

        // Категории расходов и доходов
        SqliteConFactory.Exec(conn, @"
            CREATE TABLE IF NOT EXISTS Categories (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Kind INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            )");

        // Транзакции
        SqliteConFactory.Exec(conn, @"
            CREATE TABLE IF NOT EXISTS Transactions (
                Id TEXT PRIMARY KEY,
                Date TEXT NOT NULL,
                Description TEXT NOT NULL DEFAULT '',
                CreatedAt TEXT NOT NULL
            )");

        // Проводки двойной записи — Debit/Credit
        SqliteConFactory.Exec(conn, @"
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

        // Долги и займы
        SqliteConFactory.Exec(conn, @"
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

        // Курсы валют
        SqliteConFactory.Exec(conn, @"
            CREATE TABLE IF NOT EXISTS CurrencyRates (
                CurrencyCode TEXT PRIMARY KEY,
                RateToBase REAL NOT NULL
            )");

        // Сохранённые шаблоны
        SqliteConFactory.Exec(conn, @"
            CREATE TABLE IF NOT EXISTS Templates (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Choice INTEGER NOT NULL,
                FromAccountId TEXT,
                ToAccountId TEXT,
                CategoryId TEXT,
                Amount REAL NOT NULL DEFAULT 0,
                Description TEXT NOT NULL DEFAULT ''
            )");
    }

    private void MigrateSchema()
    {
        using var conn = _factory.Open();

        if (!ColumnExists(conn, "Accounts", "IsDeleted"))
            SqliteConFactory.Exec(conn, "ALTER TABLE Accounts ADD COLUMN IsDeleted INTEGER NOT NULL DEFAULT 0");

        if (!ColumnExists(conn, "Accounts", "GroupId"))
            SqliteConFactory.Exec(conn, "ALTER TABLE Accounts ADD COLUMN GroupId TEXT");
    }

    // Проверяет наличие колонки через PRAGMA table_info
    private static bool ColumnExists(SqliteConnection conn, string table, string column)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table})";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(reader.GetOrdinal("name"))
                .Equals(column, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
