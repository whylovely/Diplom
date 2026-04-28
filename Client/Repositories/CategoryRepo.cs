using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Client.Data;
using Client.Models;
using Microsoft.Data.Sqlite;

namespace Client.Repositories;

/// Хранилище категорий, техническиех счетов
public sealed class CategoriesRepository
{
    private readonly SqliteConFactory _factory;
    public event Action? Changed;
    private void RaiseChanged() => Changed?.Invoke();

    private readonly List<Category> _categories = new();

    public IReadOnlyList<Category> All => _categories;

    public CategoriesRepository(SqliteConFactory f) => _factory = f;

    public void Load()
    {
        _categories.Clear();

        using var conn = _factory.Open();

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
    }

    public void Add(Category category)
    {
        var now = DateTimeOffset.Now;
        using var conn = _factory.Open();

        SqliteConFactory.Exec(conn, @"INSERT INTO Categories (Id, Name, Kind, CreatedAt, UpdatedAt)
                     VALUES (@Id, @Name, @Kind, @Created, @Updated)",
            ("@Id", category.Id.ToString()),
            ("@Name", category.Name),
            ("@Kind", (int)category.Kind),
            ("@Created", now.ToString("O")),
            ("@Updated", now.ToString("O")));

        _categories.Add(category);

        RaiseChanged();
    }

    public void Remove(Category category)
    {
        using var conn = _factory.Open();
        SqliteConFactory.Exec(conn, "DELETE FROM Categories WHERE Id = @Id", ("@Id", category.Id.ToString()));

        _categories.RemoveAll(c => c.Id == category.Id);
        RaiseChanged();
    }
}