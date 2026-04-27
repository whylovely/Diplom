// DTO для AuthController. Email нормализуется в lower-case на стороне сервера.
namespace Shared.Auth;

public sealed record RegisterRequest(string Email, string Password);
public sealed record LoginRequest(string Email, string Password);

/// <summary>JWT access token (HS256, 7 дней). Клиент сохраняет его в SettingsService.</summary>
public sealed record AuthResponse(string AccessToken);