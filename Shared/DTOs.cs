namespace Shared;

public sealed record MoneyDto(decimal Amount, string CurrencyCode);

public sealed record EntryDto(
    Guid Id,
    Guid AccountId,
    Guid CategoryId,
    int Direction,
    MoneyDto Amount
);

public sealed record TransactionDto(
    Guid Id,
    DateTimeOffset Date,
    string Description,
    IReadOnlyList<EntryDto> Entries
);

public sealed record CreateTransactionRequest(
    DateTimeOffset Date,
    string Description,
    IReadOnlyList<EntryDto> Entries
);