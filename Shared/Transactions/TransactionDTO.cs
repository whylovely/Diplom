namespace Shared.Transactions;

// DTO транзакций и проводок. Используются и в HTTP-API, и в SyncPushRequest.
public sealed record MoneyDto(decimal Amount, string Currency);

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