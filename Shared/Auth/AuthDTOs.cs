namespace Shared.Auth;

public sealed record RegisterRequest(string Email, string Password);
public sealed record LoginRequest(string Email, string Password);

// Запрос на обновление access token по refresh token
public sealed record RefreshRequest(string RefreshToken);

// JWT access token (HS256, 7 дней) + refresh token (60 дней)
public sealed record AuthResponse(string AccessToken, string RefreshToken);