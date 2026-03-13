using System;
using Microsoft.Data.Sqlite;

var dbPath = @"C:\Users\viner\AppData\Roaming\FinanceTracker\finance.db";
var connectionString = $"Data Source={dbPath}";

using (var conn = new SqliteConnection(connectionString))
{
    conn.Open();
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = "PRAGMA table_info(Accounts)";
        using (var reader = cmd.ExecuteReader())
        {
            Console.WriteLine("Columns in Accounts table:");
            while (reader.Read())
            {
                Console.WriteLine($"- {reader["name"]}");
            }
        }
    }
}
