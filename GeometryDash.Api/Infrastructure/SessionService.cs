using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using GeometryDash.Api.Data;
using GeometryDash.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace GeometryDash.Api.Infrastructure;

public sealed class SessionService(AppDbContext db, UserManager<AppUser> users, IConfiguration config, IWebHostEnvironment environment)
{
    public const string CookieName = "gd_refresh";

    public static string TokenHash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static object Profile(AppUser u) => new { u.Id, u.UserName, u.DisplayName, u.Email, u.Status, u.CreatedAt };

    public void RequireOrigin(HttpContext ctx)
    {
        var origin = ctx.Request.Headers.Origin.ToString();
        var allowed = config.GetSection("AllowedOrigins").Get<string[]>() ?? [];

        if (!allowed.Contains(origin, StringComparer.Ordinal))
            throw new ApiError(403, "ORIGIN_REJECTED", "Origen no permitido para esta operación de sesión.");
    }

    public async Task<object> Issue(HttpContext ctx, AppUser user, AuthSession? previous = null)
    {
        var raw = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(48));
        var session = new AuthSession
        {
            UserId = user.Id,
            RefreshTokenHash = TokenHash(raw),
            ExpiresAt = previous?.ExpiresAt ?? DateTimeOffset.UtcNow.AddDays(7),
            FamilyId = previous?.FamilyId ?? Guid.NewGuid()
        };
        db.Sessions.Add(session);
        // Persist the successor first because ReplacedBySessionId has a real foreign key.
        await db.SaveChangesAsync();

        if (previous is not null)
        {
            previous.RevokedAt = DateTimeOffset.UtcNow;
            previous.ReplacedBySessionId = session.Id;
            await db.SaveChangesAsync();
        }
        ctx.Response.Cookies.Append(CookieName, raw, CookieOptions(session.ExpiresAt));
        var roles = await users.GetRolesAsync(user);
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.Id.ToString()), new("sv", user.SecurityVersion.ToString()), new("sid", session.Id.ToString()) };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        var expires = DateTime.UtcNow.AddMinutes(15);

        if (expires > session.ExpiresAt.UtcDateTime)
            expires = session.ExpiresAt.UtcDateTime;
        var token = new JwtSecurityToken(config["Jwt:Issuer"], config["Jwt:Audience"], claims, expires: expires,
            signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!)), SecurityAlgorithms.HmacSha256));
        ctx.Response.Headers.CacheControl = "no-store";

        return new
        {
            accessToken = new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt = expires,
            user = Profile(user)
        };
    }

    public CookieOptions CookieOptions(DateTimeOffset? expires = null) => new()
    {
        HttpOnly = true,
        Secure = !environment.IsDevelopment(),
        SameSite = SameSiteMode.Strict,
        Path = "/api/v1/auth",
        Expires = expires
    };
}
