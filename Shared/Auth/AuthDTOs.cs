namespace Shared.Auth;

public sealed record RegisterRequest(string Email, string Password);
public sealed record LoginRequest(string Email, string Password);

// JWT access token (HS256, 7 дней)
public sealed record AuthResponse(string AccessToken);