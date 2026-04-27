// DTO транзакций и проводок. Используются и в HTTP-API, и в SyncPushRequest.
namespace Shared.Transactions;

/// <summary>Серверный аналог клиентского Money. Currency — ISO 4217 (3 буквы).</summary>
public sealed record MoneyDto(decimal Amount, string Currency);

/// <summary>
/// Направление проводки в DTO: Debit=0, Credit=1.
/// На сервере (EntryEntity.EntryDirection) Debit=1, Credit=2 — маппинг через cast в контроллерах.
/// </summary>
public enum EntryDirection
{
    Debit = 0,
    Credit = 1
}

public sealed record CreateEntryRequest(
    Guid AccountId,
    Guid? CategoryId,
    EntryDirection Direction,
    MoneyDto Money
);

public sealed record CreateTransactionRequest(
    DateTimeOffset Date,
    string? Description,
    IReadOnlyList<CreateEntryRequest> Entries
);

public sealed record EntryDto(
    Guid Id,
    Guid AccountId,
    Guid? CategoryId,
    EntryDirection Direction,
    MoneyDto Money
);

public sealed record TransactionDto(
    Guid Id,
    DateTimeOffset Date,
    string? Description,
    IReadOnlyList<EntryDto> Entries
);