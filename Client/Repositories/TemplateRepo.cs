using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Client.Data;
using Client.Models;
using Microsoft.Data.Sqlite;

namespace Client.Repositories;

/// <summary>
/// Хранилище шаблонов транзакций. Шаблон — это набор предзаполненных полей формы,
/// который пользователь сохраняет для часто повторяющихся операций (например, «оплата интернета»).
/// Сами шаблоны не порождают транзакции — они только подставляются в форму
/// <c>NewTransactionViewModel</c>, откуда пользователь нажимает «Сохранить».
/// </summary>
public sealed class TemplatesRepository
{
    private readonly SqliteConFactory _factory;

    public event Action? Changed;
    private void RaiseChanged() => Changed?.Invoke();

    private readonly List<TransactionTemplate> _templates = new();
    public IReadOnlyList<TransactionTemplate> All => _templates;

    public TemplatesRepository(SqliteConFactory f) => _factory = f;

    public void Load()
    {
        _templates.Clear();

        using var conn = _factory.Open();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT * FROM Templates";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                _templates.Add(new TransactionTemplate
                {
                    Id = Guid.Parse(r.GetString(r.GetOrdinal("Id"))),
                    Name = r.GetString(r.GetOrdinal("Name")),
                    Choice = (TxKindChoice)r.GetInt32(r.GetOrdinal("Choice")),
                    FromAccountId = r.IsDBNull(r.GetOrdinal("FromAccountId")) ? null : Guid.Parse(r.GetString(r.GetOrdinal("FromAccountId"))),
                    ToAccountId = r.IsDBNull(r.GetOrdinal("ToAccountId")) ? null : Guid.Parse(r.GetString(r.GetOrdinal("ToAccountId"))),
                    CategoryId = r.IsDBNull(r.GetOrdinal("CategoryId")) ? null : Guid.Parse(r.GetString(r.GetOrdinal("CategoryId"))),
                    Amount = (decimal)r.GetDouble(r.GetOrdinal("Amount")),
                    Description = r.GetString(r.GetOrdinal("Description"))
                });
            }
        }
    }

    public Task Add(TransactionTemplate template)
    {
        using var conn = _factory.Open();
        SqliteConFactory.Exec(conn, @"INSERT INTO Templates (Id, Name, Choice, FromAccountId, ToAccountId, CategoryId, Amount, Description)
                     VALUES (@Id, @Name, @Choice, @From, @To, @Cat, @Amt, @Desc)",
            ("@Id", template.Id.ToString()),
            ("@Name", template.Name),
            ("@Choice", (int)template.Choice),
            ("@From", (object?)template.FromAccountId?.ToString() ?? DBNull.Value),
            ("@To", (object?)template.ToAccountId?.ToString() ?? DBNull.Value),
            ("@Cat", (object?)template.CategoryId?.ToString() ?? DBNull.Value),
            ("@Amt", (double)template.Amount),
            ("@Desc", template.Description));

        _templates.Add(template);
        RaiseChanged();
        return Task.CompletedTask;
    }

    public Task Delete(Guid id)
    {
        using var conn = _factory.Open();
        SqliteConFactory.Exec(conn, "DELETE FROM Templates WHERE Id = @Id", ("@Id", id.ToString()));

        _templates.RemoveAll(t => t.Id == id);
        RaiseChanged();
        return Task.CompletedTask;
    }
}