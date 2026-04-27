namespace Server.Entities;

/// <summary>
/// Пользователь системы. Email хранится в lower-case (нормализуется в AuthController),
/// PasswordHash — PBKDF2 от ASP.NET Identity. Role = "User" по умолчанию, "Admin" для администраторов.
/// IsBlocked — флаг блокировки администратором, не позволяет логиниться.
/// </summary>
public sealed class UserEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Email { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;

    public string Role { get; set; } = "User";

    public bool IsBlocked { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}