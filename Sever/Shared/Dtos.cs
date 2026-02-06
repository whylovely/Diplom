namespace Shared;

public sealed record AccountDto(Guid Id, string Name, string CurrencyCode, int Type, decimal InitialBalance);
public sealed record CategoryDto(Guid Id, string Name);

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