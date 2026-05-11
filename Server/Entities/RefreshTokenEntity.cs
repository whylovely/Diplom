namespace Server.Entities;

/// <summary>
/// Refresh token пользователя. Хранится в БД, чтобы можно было отозвать.
/// При обновлении старый токен помечается IsRevoked=true и выдаётся новая пара.
/// </summary>
public sealed class RefreshTokenEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public UserEntity User { get; set; } = default!;

    // SHA-256 хеш токена — в БД храним хеш, не сам токен
    public string TokenHash { get; set; } = default!;

    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsRevoked { get; set; }
}
