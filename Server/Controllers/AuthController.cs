using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Server.Data;
using Server.Entities;
using Shared.Auth;

namespace Server.Controllers;

/// <summary>
/// Регистрация и вход пользователей. Выдаёт JWT с claim'ами Sub/Email/NameIdentifier/Role,
/// срок жизни 7 дней. Пароли хешируются <see cref="PasswordHasher{T}"/> (PBKDF2).
/// Email нормализуется в lower-case до записи и поиска — чтобы Bob@x.com и bob@x.com были одним юзером.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PasswordHasher<UserEntity> _hasher = new();
    private readonly IConfiguration _cfg;

    public AuthController(AppDbContext db, IConfiguration cfg)
    {
        _db = db;
        _cfg = cfg;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest("Email and password are required.");

        var exists = await _db.Users.AnyAsync(x => x.Email == email, ct);
        if (exists) return Conflict("User already exists.");

        var user = new UserEntity { Email = email };
        user.PasswordHash = _hasher.HashPassword(user, req.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return Ok(new AuthResponse(CreateToken(user)));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.SingleOrDefaultAsync(x => x.Email == email, ct);
        if (user is null) return Unauthorized("Invalid credentials.");
        if (user.IsBlocked) return Unauthorized("Account is blocked.");

        var vr = _hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password);
        if (vr == PasswordVerificationResult.Failed) return Unauthorized("Invalid credentials.");

        return Ok(new AuthResponse(CreateToken(user)));
    }

    /// <summary>
    /// Отдельный endpoint для входа в админ-панель: проверяет роль "Admin",
    /// иначе возвращает 403, даже если пароль правильный. Это защищает от случайного
    /// открытия админки обычным пользователем.
    /// </summary>
    [HttpPost("admin/login")]
    public async Task<ActionResult<AuthResponse>> AdminLogin(LoginRequest req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.SingleOrDefaultAsync(x => x.Email == email, ct);
        if (user is null) return Unauthorized("Invalid credentials.");
        if (user.Role != "Admin") return Forbid();

        var vr = _hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password);
        if (vr == PasswordVerificationResult.Failed) return Unauthorized("Invalid credentials.");

        return Ok(new AuthResponse(CreateToken(user)));
    }

    // Формирует подписанный JWT (HS256). Settings вынесены в appsettings.json (Jwt:Issuer/Audience/Key),
    // в тестах ключ переопределяется через PostConfigure<JwtBearerOptions>.
    private string CreateToken(UserEntity user)
    {
        var jwt = _cfg.GetSection("Jwt");
        var issuer = jwt["Issuer"]!;
        var audience = jwt["Audience"]!;
        var key = jwt["Key"]!;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Role, user.Role)
        };

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}