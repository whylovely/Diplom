using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace Client.Data;

/// <summary>
/// Фабрика SQLite-подключений. Единственное место в проекте, где формируется
/// строка подключения и путь к файлу БД (%AppData%/Diplom/finance.db).
/// Все репозитории получают её через DI и открывают соединения через <see cref="Open"/>.
/// </summary>
public sealed class SqliteConFactory
{
    public string ConnectionString { get; }
    public string DbPath { get; }

    public SqliteConFactory()
    {
        // Локальная БД лежит в %AppData%/Diplom/finance.db.
        // Папка создаётся, если её ещё нет — иначе SQLite упадёт при первом подключении.
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "Diplom");
        Directory.CreateDirectory(dir);
        DbPath = Path.Combine(dir, "finance.db");
        ConnectionString = $"Data Source={DbPath}";
    }

    /// <summary>Открывает новое соединение. Вызывающий обязан его закрыть (через using).</summary>
    public SqliteConnection Open()
    {
        var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        return conn;
    }

    /// <summary>
    /// Удобная обёртка для одноразовых параметризованных INSERT/UPDATE/DELETE.
    /// Параметры передаются кортежами (имя, значение); null заменяется на DBNull.
    /// </summary>
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
