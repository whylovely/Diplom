using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Server.Data;
using Server.Entities;
using Shared.Auth;

namespace Server.Controllers;

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

        return Ok(await IssueTokenPairAsync(user, ct));
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

        return Ok(await IssueTokenPairAsync(user, ct));
    }

    [HttpPost("admin/login")]
    public async Task<ActionResult<AuthResponse>> AdminLogin(LoginRequest req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.SingleOrDefaultAsync(x => x.Email == email, ct);
        if (user is null) return Unauthorized("Invalid credentials.");
        if (user.Role != "Admin") return Forbid();

        var vr = _hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password);
        if (vr == PasswordVerificationResult.Failed) return Unauthorized("Invalid credentials.");

        return Ok(await IssueTokenPairAsync(user, ct));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.RefreshToken))
            return BadRequest("Refresh token is required.");

        var hash = HashToken(req.RefreshToken);

        var stored = await _db.RefreshTokens
            .Include(x => x.User)
            .SingleOrDefaultAsync(x => x.TokenHash == hash, ct);

        if (stored is null || stored.IsRevoked || stored.ExpiresAt < DateTimeOffset.UtcNow)
            return Unauthorized("Refresh token is invalid or expired.");

        if (stored.User.IsBlocked)
            return Unauthorized("Account is blocked.");

        // Отзываем старый токен и сразу выдаём новую пару
        stored.IsRevoked = true;
        await _db.SaveChangesAsync(ct);

        return Ok(await IssueTokenPairAsync(stored.User, ct));
    }

    private async Task<AuthResponse> IssueTokenPairAsync(UserEntity user, CancellationToken ct)
    {
        var accessToken  = CreateAccessToken(user);
        var refreshToken = GenerateRefreshToken();

        var stale = _db.RefreshTokens
            .Where(x => x.UserId == user.Id && (x.IsRevoked || x.ExpiresAt < DateTimeOffset.UtcNow));
        _db.RefreshTokens.RemoveRange(stale);

        _db.RefreshTokens.Add(new RefreshTokenEntity
        {
            UserId    = user.Id,
            TokenHash = HashToken(refreshToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(60)
        });

        await _db.SaveChangesAsync(ct);

        return new AuthResponse(accessToken, refreshToken);
    }

    private string CreateAccessToken(UserEntity user)
    {
        var jwt = _cfg.GetSection("Jwt");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Role, user.Role)
        };

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
