using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace Client.Data;

public sealed class SqliteConFactory
{
    public string ConnectionString { get; }
    public string DbPath { get; }

    public SqliteConFactory()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "Diplom");
        Directory.CreateDirectory(dir);
        DbPath = Path.Combine(dir, "finance.db");
        ConnectionString = $"Data Source={DbPath}";
    }

    public SqliteConnection Open()
    {
        var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        return conn;
    }

    public static void Exec(SqliteConnection conn, string sql,
        params (string name, object value)[] parameters)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (n, v) in parameters)
            cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }
}