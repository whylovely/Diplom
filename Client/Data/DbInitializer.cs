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

    /// <summary>Полная инициализация: схема + миграции.</summary>
    public void Initialize()
    {
        EnsureCreated();
        MigrateSchema();
    }

    /// <summary>
    /// Создаёт все таблицы через CREATE TABLE IF NOT EXISTS — повторный вызов безопасен.
    /// Здесь хранится «эталонная» схема БД на момент текущей версии клиента.
    /// </summary>
    private void EnsureCreated()
    {
        using var conn = _factory.Open();

        // Счета пользователя: активы, технические доходы и расходы
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

        // Группы счетов — для отображения в боковой панели
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

        // Транзакции — заголовки операций
        SqliteConFactory.Exec(conn, @"
            CREATE TABLE IF NOT EXISTS Transactions (
                Id TEXT PRIMARY KEY,
                Date TEXT NOT NULL,
                Description TEXT NOT NULL DEFAULT '',
                CreatedAt TEXT NOT NULL
            )");

        // Проводки двойной записи — Debit/Credit, привязка к счёту и категории
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

        // Долги и займы — кому я должен или мне должны
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

        // Курсы валют к базовой (по умолчанию RUB = 1.0)
        SqliteConFactory.Exec(conn, @"
            CREATE TABLE IF NOT EXISTS CurrencyRates (
                CurrencyCode TEXT PRIMARY KEY,
                RateToBase REAL NOT NULL
            )");

        // Сохранённые шаблоны частых операций
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

    /// <summary>
    /// Доливает колонки, которые появились в более поздних версиях схемы.
    /// SQLite не поддерживает «IF NOT EXISTS» для ALTER TABLE, поэтому сначала
    /// проверяем PRAGMA table_info, и только потом добавляем колонку.
    /// </summary>
    private void MigrateSchema()
    {
        using var conn = _factory.Open();

        // Колонка IsDeleted появилась после введения мягкого удаления счетов
        if (!ColumnExists(conn, "Accounts", "IsDeleted"))
            SqliteConFactory.Exec(conn, "ALTER TABLE Accounts ADD COLUMN IsDeleted INTEGER NOT NULL DEFAULT 0");

        // Колонка GroupId — для группировки счетов в боковой панели
        if (!ColumnExists(conn, "Accounts", "GroupId"))
            SqliteConFactory.Exec(conn, "ALTER TABLE Accounts ADD COLUMN GroupId TEXT");
    }

    /// <summary>Проверяет наличие колонки через PRAGMA table_info.</summary>
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
