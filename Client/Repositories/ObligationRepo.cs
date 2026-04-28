using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Client.Data;
using Client.Models;
using Microsoft.Data.Sqlite;

namespace Client.Repositories;

// Хранилище обязательств
public sealed class ObligationRepository
{
    private readonly SqliteConFactory _factory;
    public event Action? Changed;
    private void RaiseChanged() => Changed?.Invoke();

    private readonly List<Obligation> _obligations = new();
    public IReadOnlyList<Obligation> All => _obligations;

    public ObligationRepository(SqliteConFactory f) => _factory = f;

    public void Load()
    {
        _obligations.Clear();

        using var conn = _factory.Open();
        
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

    public Task Add(Obligation obligation)
    {
        using var conn = _factory.Open();
        
        SqliteConFactory.Exec(conn, @"INSERT INTO Obligations (Id, Counterparty, Amount, Currency, Type, CreatedAt, DueDate, IsPaid, PaidAt, Note)
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

    public Task Update(Obligation obligation)
    {
        using var conn = _factory.Open();
        SqliteConFactory.Exec(conn, @"UPDATE Obligations SET Counterparty=@Cp, Amount=@Amt, Currency=@Cur, Type=@Type,
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

    public Task Delete(Guid id)
    {
        using var conn = _factory.Open();
        SqliteConFactory.Exec(conn, "DELETE FROM Obligations WHERE Id = @Id", ("@Id", id.ToString()));

        _obligations.RemoveAll(o => o.Id == id);
        RaiseChanged();
        return Task.CompletedTask;
    }

    public Task Mark(Guid id, bool isPaid)
    {
        var existing = _obligations.FirstOrDefault(o => o.Id == id);
        if (existing == null) return Task.CompletedTask;

        existing.IsPaid = isPaid;
        existing.PaidAt = isPaid ? DateTimeOffset.Now : null;

        using var conn = _factory.Open();
        SqliteConFactory.Exec(conn, "UPDATE Obligations SET IsPaid=@Paid, PaidAt=@PaidAt WHERE Id=@Id",
            ("@Paid", isPaid ? 1 : 0),
            ("@PaidAt", existing.PaidAt.HasValue ? (object)existing.PaidAt.Value.ToString("O") : DBNull.Value),
            ("@Id", id.ToString()));

        RaiseChanged();
        return Task.CompletedTask;
    }
}