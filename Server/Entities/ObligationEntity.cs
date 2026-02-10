using System;

namespace Server.Entities;

public class ObligationEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid UserId { get; set; }
    public UserEntity User { get; set; } = default!;

    public required string Counterparty { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "RUB";
    public int Type { get; set; } // 0 = я должен, 1 = мне должны

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DueDate { get; set; }

    public bool IsPaid { get; set; }
    public DateTimeOffset? PaidAt { get; set; }

    public string? Note { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
