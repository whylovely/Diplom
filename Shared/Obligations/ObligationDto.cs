using System;

namespace Shared.Obligations;

public enum ObligationType
{
    Debt = 0,   // Я должен
    Credit = 1  // Мне должны
}

public sealed record ObligationDto(
    Guid Id,
    string Counterparty,
    decimal Amount,
    string Currency,
    ObligationType Type,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DueDate,
    bool IsPaid,
    DateTimeOffset? PaidAt,
    string? Note
);

public sealed record CreateObligationRequest(
    string Counterparty,
    decimal Amount,
    string Currency,
    ObligationType Type,
    DateTimeOffset? DueDate,
    string? Note
);

public sealed record UpdateObligationRequest(
    string Counterparty,
    decimal Amount,
    string Currency,
    ObligationType Type,
    DateTimeOffset? DueDate,
    bool IsPaid,
    string? Note
);
