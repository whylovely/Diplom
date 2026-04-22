using System;
using System.Collections.Generic;
using Client.Models;
using Microsoft.Data.Sqlite;

namespace Client.Data;

public sealed class DbSeeder
{
    private readonly SqliteConFactory _factory;
    public DbSeeder(SqliteConFactory f) => _factory = f;

    public void SeedIfEmpty()
    {
        using var conn = _factory.Open();

        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT COUNT(*) FROM CurrencyRates";
        var ratesCount = (long)cmd.ExecuteScalar()!;
        if (ratesCount == 0)
        {
            var defaultRates = new Dictionary<string, double>
            {
                { "RUB", 1.0 }, { "USD", 72.0 }, { "EUR", 91.0 }, { "KZT", 0.16 },
                { "GBP", 104.0 }, { "CNY", 12.0 }, { "TRY", 1.8 }
            };

            foreach (var kvp in defaultRates)
            {
                SqliteConFactory.Exec(conn, "INSERT INTO CurrencyRates (CurrencyCode, RateToBase) VALUES (@Code, @Rate)",
                    ("@Code", kvp.Key), ("@Rate", kvp.Value));
            }
        }

        cmd.CommandText = "SELECT COUNT(*) FROM Accounts";
        var accsCount = (long)cmd.ExecuteScalar()!;
        if (accsCount == 0)
        {
            var now = DateTimeOffset.Now.ToString("O");

            var catSalaryId    = Guid.NewGuid().ToString();
            var accIncomeSalaryId  = Guid.NewGuid().ToString();
            var accExpenseSalaryId = Guid.NewGuid().ToString();

            var catFoodId      = Guid.NewGuid().ToString();
            var accIncomeFoodId    = Guid.NewGuid().ToString();
            var accExpenseFoodId   = Guid.NewGuid().ToString();

            var catTransportId = Guid.NewGuid().ToString();
            var accIncomeTransportId  = Guid.NewGuid().ToString();
            var accExpenseTransportId = Guid.NewGuid().ToString();

            void AddCategory(string id, string name, int kind, string incomeId, string expenseId)
            {
                SqliteConFactory.Exec(conn, @"INSERT INTO Categories (Id, Name, Kind, CreatedAt, UpdatedAt) VALUES (@Id, @Name, @Kind, @Now, @Now)",
                    ("@Id", id), ("@Name", name), ("@Kind", kind), ("@Now", now));

                SqliteConFactory.Exec(conn, @"INSERT INTO Accounts (Id, Name, CurrencyCode, InitialBalance, Balance, Type, AccountMultiType, SecondaryBalance, CreatedAt, UpdatedAt)
                             VALUES (@Id, @Name, 'RUB', 0, 0, 2, 0, 0, @Now, @Now)",
                             ("@Id", expenseId), ("@Name", $"Расходы: {name}"), ("@Now", now));

                SqliteConFactory.Exec(conn, @"INSERT INTO Accounts (Id, Name, CurrencyCode, InitialBalance, Balance, Type, AccountMultiType, SecondaryBalance, CreatedAt, UpdatedAt)
                             VALUES (@Id, @Name, 'RUB', 0, 0, 1, 0, 0, @Now, @Now)",
                             ("@Id", incomeId), ("@Name", $"Доходы: {name}"), ("@Now", now));
            }

            AddCategory(catSalaryId,    "Зарплата",  1, accIncomeSalaryId,    accExpenseSalaryId);
            AddCategory(catFoodId,      "Продукты",  0, accIncomeFoodId,      accExpenseFoodId);
            AddCategory(catTransportId, "Транспорт", 0, accIncomeTransportId, accExpenseTransportId);

            var act1 = Guid.NewGuid().ToString();
            var act2 = Guid.NewGuid().ToString();

            SqliteConFactory.Exec(conn, @"INSERT INTO Accounts (Id, Name, CurrencyCode, InitialBalance, Balance, Type, AccountMultiType, SecondaryBalance, CreatedAt, UpdatedAt)
                         VALUES (@Id, 'Наличные', 'RUB', 50000, 39500, 0, 0, 0, @Now, @Now)",
                         ("@Id", act1), ("@Now", now));
            SqliteConFactory.Exec(conn, @"INSERT INTO Accounts (Id, Name, CurrencyCode, InitialBalance, Balance, Type, AccountMultiType, SecondaryBalance, CreatedAt, UpdatedAt)
                         VALUES (@Id, 'Заначка USD', 'USD', 1000, 1000, 0, 0, 0, @Now, @Now)",
                         ("@Id", act2), ("@Now", now));

            SqliteConFactory.Exec(conn, @"INSERT INTO Obligations (Id, Counterparty, Amount, Currency, Type, CreatedAt, IsPaid)
                         VALUES (@Id, 'Иван', 5000, 'RUB', 0, @Now, 0)",
                         ("@Id", Guid.NewGuid().ToString()), ("@Now", now));

            void AddTx(string dateDelta, string desc, string accId, string catId, string techAccId, EntryDirection mainAccDir, double amount, string currency)
            {
                var txId   = Guid.NewGuid().ToString();
                var txDate = DateTimeOffset.Now.AddDays(int.Parse(dateDelta)).ToString("O");

                SqliteConFactory.Exec(conn, @"INSERT INTO Transactions (Id, Date, Description, CreatedAt) VALUES (@Id, @Date, @Desc, @Now)",
                    ("@Id", txId), ("@Date", txDate), ("@Desc", desc), ("@Now", now));

                SqliteConFactory.Exec(conn, @"INSERT INTO Entries (Id, TransactionId, AccountId, CategoryId, Direction, Amount, CurrencyCode)
                             VALUES (@Id, @TxId, @AccId, @CatId, @Dir, @Amt, @Cur)",
                             ("@Id", Guid.NewGuid().ToString()), ("@TxId", txId), ("@AccId", accId),
                             ("@CatId", catId), ("@Dir", (int)mainAccDir), ("@Amt", amount), ("@Cur", currency));

                var techDir = mainAccDir == EntryDirection.Debit ? EntryDirection.Credit : EntryDirection.Debit;
                SqliteConFactory.Exec(conn, @"INSERT INTO Entries (Id, TransactionId, AccountId, CategoryId, Direction, Amount, CurrencyCode)
                             VALUES (@Id, @TxId, @AccId, @CatId, @Dir, @Amt, @Cur)",
                             ("@Id", Guid.NewGuid().ToString()), ("@TxId", txId), ("@AccId", techAccId),
                             ("@CatId", catId), ("@Dir", (int)techDir), ("@Amt", amount), ("@Cur", currency));
            }

            AddTx("-5", "Аванс",      act1, catSalaryId,    accIncomeSalaryId,    EntryDirection.Debit,  50000, "RUB");
            AddTx("-2", "Лента",       act1, catFoodId,      accExpenseFoodId,     EntryDirection.Credit,  8500, "RUB");
            AddTx("-1", "Пятерочка",  act1, catFoodId,      accExpenseFoodId,     EntryDirection.Credit,  1200, "RUB");
            AddTx("0",  "Такси",      act1, catTransportId, accExpenseTransportId, EntryDirection.Credit,   800, "RUB");
        }
    }
}
